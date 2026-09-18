using GiftLists.Contracts.GiftLists.Events;
using Reservations.Application.GiftLists;
using Reservations.Contracts.Reservations;
using Reservations.Contracts.Reservations.Events;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// <see cref="GiftReservedV1"/> landing on a real broker under its expected type name — the only
/// way to prove Rebus's own type-name-based routing/deserialization (CONVENTIONS.md "Testing")
/// still lines up with what a subscriber (the Gateway, in production) actually asks to subscribe
/// to. An in-memory transport would hand the object straight to a handler and never touch this.
/// Mirrors <c>Identity.IntegrationTests.Users.UserEventPublishingTests</c> exactly.
///
/// <see cref="GiftReservedV1Tests"/> (Reservations.UnitTests) already pins this event's SHAPE —
/// exactly three fields, never a reservation id or a release secret — by reflection alone; this
/// suite is only responsible for proving the real publish/subscribe path still works, not for
/// re-deriving that shape guarantee against a slower, flakier real broker. GL-37 adds one more
/// thing this suite alone can prove — that the ACTUAL, wire-deserialized event a real subscriber
/// receives for a real reservation still carries nothing that could unmask it — since the unit
/// test can only pin the static shape of the type, never a value drawn from a live reservation's
/// own minted <see cref="ReserveGiftReply.ReleaseSecret"/>.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class GiftReservedV1PublishingTests(ReservationsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReserveGift_ShouldPublishGiftReservedV1_UnderItsOwnTypeName_ToARealSubscriber_WithNoReserverIdentity()
    {
        // Arrange — a subscriber wired up exactly the way the Gateway would be: it asks Rebus to
        // subscribe to GiftReservedV1 by .NET type, and only receives anything at all if the
        // publisher's wire type name still matches what the subscription was registered under.
        await using var subscriber = await EventSubscriber<GiftReservedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddDays(7), "ShareTokenPlaceholder", DateTimeOffset.UtcNow));
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
        await WaitForItemOnProjectionAsync(listId, itemId);

        // Act
        var reserved = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));
        Assert.True(reserved.IsSuccess);
        var (received, typeHeader) = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(listId, received.ListId);
        Assert.Equal(itemId, received.ItemId);
        Assert.NotNull(typeHeader);
        Assert.Contains(nameof(GiftReservedV1), typeHeader, StringComparison.Ordinal);

        // GL-37: "no reserver identity" proven two ways against the actual instance a real
        // subscriber received, not the type's static shape (GiftReservedV1Tests already covers
        // that) — first, that its public properties are exactly the three known ones (a future
        // field under any name at all would fail this, whatever it was called); second, reflecting
        // over those properties' VALUES to confirm none of them equals or contains this specific
        // reservation's own release secret, so the check does not depend on ever naming the field
        // "ReleaseSecret" to begin with.
        var propertyNames = typeof(GiftReservedV1).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "ListId", "ItemId", "ReservedAt" }, propertyNames);

        var secret = reserved.Value.ReleaseSecret;
        foreach (var property in typeof(GiftReservedV1).GetProperties())
        {
            var value = property.GetValue(received)?.ToString() ?? string.Empty;
            Assert.DoesNotContain(secret, value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Waits for this service's own gift-list projection to reflect the item just added, before
    /// sending <see cref="ReserveGift"/> — GL-34's handler and this test's own publish both race
    /// over the same real broker, so without this wait the reserve can (and does) arrive before
    /// the projection exists at all.
    /// </summary>
    private async Task WaitForItemOnProjectionAsync(Guid listId, Guid itemId)
    {
        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListProjectionRepository>();
        await Eventually.Async(
            () => repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None),
            projection => projection?.ItemIds.Contains(new GiftItemId(itemId)) == true,
            TimeSpan.FromSeconds(15));
    }
}
