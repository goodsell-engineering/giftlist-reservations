using GiftLists.Contracts.GiftLists.Events;
using Reservations.Application.GiftLists;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reservations.IntegrationTests.GiftLists;

/// <summary>
/// GL-34 shipped <c>GiftListCreatedV1Handler</c>/<c>GiftListDeletedV1Handler</c>/
/// <c>GiftItemAddedV1Handler</c>/<c>GiftItemRemovedV1Handler</c> with no coverage at all (folded
/// into GL-36 from the Batch 37 review). Each test here publishes the real
/// <c>GiftLists.Contracts</c> event onto the real broker — never calls a handler or interactor
/// directly — and asserts on the real Mongo projection those handlers build (CONVENTIONS.md
/// "Testing": all infrastructure real via Testcontainers; ARCHITECTURE.md "Consuming other
/// services' events: anti-corruption layer").
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class GiftListProjectionConsumptionTests(ReservationsFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Mirrors the internal <c>GiftListProjectionRepository.CollectionName</c> — not accessible from here, kept in sync by hand.</summary>
    private const string GiftListProjectionsCollectionName = "giftListProjections";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GiftListCreatedV1_ShouldPopulateTheProjection_WhenPublishedOverTheRealBroker()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", expiresAt, "ShareTokenPlaceholder", DateTimeOffset.UtcNow));
        var projection = await WaitForProjectionAsync(listId, p => p is not null);

        // Assert
        Assert.NotNull(projection);
        Assert.False(projection.IsDeleted);
        Assert.Empty(projection.ItemIds);
        // Millisecond precision only (ProjectionInstants) — compare within a second rather than
        // for bit-for-bit equality with the DateTimeOffset this test constructed.
        Assert.True(Math.Abs((projection.ExpiresAt - expiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public async Task GiftListDeletedV1_ShouldMarkTheProjectionDeleted_WhenPublishedOverTheRealBroker()
    {
        // Arrange
        var listId = await CreateListAsync();

        // Act
        await fixture.GiftListsBus.Publish(new GiftListDeletedV1(listId, DateTimeOffset.UtcNow));
        var projection = await WaitForProjectionAsync(listId, p => p?.IsDeleted == true);

        // Assert — still a row (IGiftListProjectionRepository.FindByIdAsync's own doc comment:
        // unlike Gateway's, a deleted list here is still returned, with IsDeleted true), not gone.
        Assert.NotNull(projection);
        Assert.True(projection.IsDeleted);
    }

    [Fact]
    public async Task GiftItemAddedV1_ShouldAddTheItemToTheProjection_WhenPublishedOverTheRealBroker()
    {
        // Arrange
        var listId = await CreateListAsync();
        var itemId = Guid.NewGuid();

        // Act
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(listId, itemId, "Lego Set", "The big one", "https://example.test/lego", DateTimeOffset.UtcNow));
        var projection = await WaitForProjectionAsync(listId, p => p?.ItemIds.Count > 0);

        // Assert
        var item = Assert.Single(projection!.ItemIds);
        Assert.Equal(new GiftItemId(itemId), item);
    }

    [Fact]
    public async Task GiftItemRemovedV1_ShouldRemoveTheItemFromTheProjection_WhenPublishedOverTheRealBroker()
    {
        // Arrange
        var listId = await CreateListAsync();
        var itemId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
        await WaitForProjectionAsync(listId, p => p?.ItemIds.Count > 0);

        // Act
        await fixture.GiftListsBus.Publish(new GiftItemRemovedV1(listId, itemId, DateTimeOffset.UtcNow));
        var projection = await WaitForProjectionAsync(listId, p => p?.ItemIds.Count == 0);

        // Assert
        Assert.Empty(projection!.ItemIds);
    }

    /// <summary>
    /// The hazard GL-34's own doc comments call out by name: at-least-once, out-of-order delivery
    /// (CONVENTIONS.md "Messaging") means a <c>GiftItemRemovedV1</c> can be processed BEFORE its
    /// matching <c>GiftItemAddedV1</c> for the same item — the remove must leave a tombstone the
    /// later add loses against, not silently resurrect the item.
    /// </summary>
    [Fact]
    public async Task GiftItemAddedV1_ShouldNotResurrectTheItem_WhenItArrivesAfterAGiftItemRemovedV1ForTheSameItem()
    {
        // Arrange
        var listId = await CreateListAsync();
        var itemId = Guid.NewGuid();
        var addedAt = DateTimeOffset.UtcNow;
        var removedAt = addedAt.AddSeconds(5); // chronologically later than the add below

        // Act — the remove is processed first (its message simply arrives first, before any add
        // has ever been seen for this item); only once that tombstone is confirmed written is the
        // (chronologically stale) add published, so this does not depend on Rebus's own delivery
        // ordering to reproduce the reordering GL-34's own doc comment describes.
        await fixture.GiftListsBus.Publish(new GiftItemRemovedV1(listId, itemId, removedAt));
        await WaitForRawItemTombstoneAsync(listId, itemId);

        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(listId, itemId, "Lego Set", null, null, addedAt));

        // Assert — give the (stale, and therefore rejected) add a chance to land, then confirm
        // the item never resurfaces. There is no positive signal to wait on here — the add is
        // expected to cause no write at all — so this polls the negative for the same window
        // every other wait in this suite uses, rather than a fixed sleep.
        var projection = await WaitForProjectionAsync(listId, p => p is not null);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var stillEmpty = await FindProjectionAsync(listId);
        Assert.NotNull(stillEmpty);
        Assert.Empty(stillEmpty.ItemIds);
        Assert.Empty(projection!.ItemIds);
    }

    private async Task<Guid> CreateListAsync()
    {
        var listId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddDays(7), "ShareTokenPlaceholder", DateTimeOffset.UtcNow));
        await WaitForProjectionAsync(listId, p => p is not null);
        return listId;
    }

    private async Task<GiftListProjection?> WaitForProjectionAsync(Guid listId, Func<GiftListProjection?, bool> isReady) =>
        await Eventually.Async(() => FindProjectionAsync(listId), isReady, WaitTimeout);

    private async Task<GiftListProjection?> FindProjectionAsync(Guid listId)
    {
        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListProjectionRepository>();
        return await repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None);
    }

    /// <summary>
    /// Reads the projection's own persisted state directly (CONVENTIONS.md "Testing" permits
    /// asserting on infrastructure directly, not just through the public surface) — needed here
    /// because a removed-but-never-added item's tombstone is, by design, never observable through
    /// <see cref="IGiftListProjectionRepository.FindByIdAsync"/>
    /// (<c>GiftListProjectionDocumentMapper</c> filters it out); this is the only way to know the
    /// remove actually landed before publishing the reordered add.
    /// </summary>
    private async Task WaitForRawItemTombstoneAsync(Guid listId, Guid itemId)
    {
        var collection = fixture.Database.GetCollection<BsonDocument>(GiftListProjectionsCollectionName);

        await Eventually.Async(
            async () => await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", listId)).FirstOrDefaultAsync(),
            document =>
            {
                if (document is null || !document.Contains("items"))
                {
                    return false;
                }

                return document["items"].AsBsonArray
                    .Select(i => i.AsBsonDocument)
                    .Any(i => i["itemId"].AsGuid == itemId && i["isRemoved"].AsBoolean);
            },
            WaitTimeout);
    }
}
