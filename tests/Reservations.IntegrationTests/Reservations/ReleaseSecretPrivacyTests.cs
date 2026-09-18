using GiftLists.Contracts.GiftLists.Events;
using Reservations.Application.GiftLists;
using Reservations.Contracts.Reservations;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// GL-37: proves ARCHITECTURE.md "Reservation privacy"'s guarantee about
/// <see cref="ReleaseSecret"/> end to end, against the real infrastructure it is supposed to hold
/// against (CONVENTIONS.md "Testing") — that the secret is returned to the reserving caller and
/// stored in <c>reservation.reservations</c> (both required, ARCHITECTURE.md "Data model"), but
/// never anywhere else this service can write to or log from. Complements
/// <see cref="GiftReservedV1PublishingTests"/>, which proves the same guarantee for the ONE
/// integration event this service publishes; this file is about everything else it can leak
/// through — its own database and its own logs.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class ReleaseSecretPrivacyTests(ReservationsFixture fixture) : IAsyncLifetime
{
    private const string ShareTokenPlaceholder = "ShareTokenPlaceholder";

    /// <summary>
    /// Mirrors the internal <c>ReservationRepository.CollectionName</c> — not accessible from here
    /// (CONVENTIONS.md "Reaching an internal from a test": neither extractable nor genuinely
    /// public), kept in sync by hand, same as <c>Identity.IntegrationTests.Fixtures.IdentityFixture.UsersCollectionName</c>'s
    /// own doc comment.
    /// </summary>
    private const string ReservationsCollectionName = "reservations";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReserveGift_ShouldStoreTheReleaseSecretInMongo_ButNeverInAnyProjectionCollection()
    {
        // Arrange
        var (listId, itemId) = await CreateReservableListAsync();

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));
        Assert.True(result.IsSuccess);
        var secret = result.Value.ReleaseSecret;

        // Assert — ARCHITECTURE.md "Data model" requires the secret in
        // reservation.reservations itself; read the raw BSON, not through
        // IReservationRepository/ReservationDocument, so a stray typed field mapped under a
        // different name could not hide a leak from this assertion the way it could from a
        // strongly-typed round trip.
        var reservations = fixture.Database.GetCollection<BsonDocument>(ReservationsCollectionName);
        var stored = await reservations
            .Find(Builders<BsonDocument>.Filter.Eq("listId", listId) & Builders<BsonDocument>.Filter.Eq("itemId", itemId))
            .FirstOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.Equal(secret, stored["releaseSecret"].AsString);

        // Every OTHER collection in this service's own database — today just
        // reservation.giftListProjections (GL-34) — must carry nothing resembling the secret
        // anywhere in any document, scanned generically (every collection, every field) rather
        // than by naming the one projection that happens to exist today, so a second projection
        // added later is covered by construction rather than by someone remembering to extend
        // this test.
        var collectionNames = await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync();
        foreach (var collectionName in collectionNames.Where(n => n != ReservationsCollectionName))
        {
            var documents = await fixture.Database
                .GetCollection<BsonDocument>(collectionName)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync();
            foreach (var document in documents)
            {
                Assert.DoesNotContain(secret, document.ToJson(), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// The <c>Logging&lt;,&gt;</c> decorator only ever logs <c>Error.Code</c>/<c>ErrorKind</c> on
    /// failure and a bare "succeeded" message otherwise (its own doc comment) — this never comes
    /// close to a response payload today. This test exists so a future change that DOES start
    /// logging a response (a debugging aid someone adds and forgets to strip) fails loudly rather
    /// than silently reintroducing GL-37, per <see cref="LogCapture"/>'s own doc comment on why a
    /// log-content assertion is the actual backstop here, not the redacting
    /// <see cref="ReleaseSecret.ToString"/> alone.
    /// </summary>
    [Fact]
    public async Task ReserveGift_ShouldNeverLogTheReleaseSecretsPlaintext()
    {
        // Arrange
        var (listId, itemId) = await CreateReservableListAsync();

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));
        Assert.True(result.IsSuccess);
        var secret = result.Value.ReleaseSecret;

        // Assert
        Assert.DoesNotContain(fixture.Logs.Entries, entry => entry.Message.Contains(secret, StringComparison.Ordinal));

        // ...and the redacting value object itself still holds, belt and braces
        // (ReleaseSecretTests already pins this in isolation; asserted again here against the
        // exact string this reservation actually minted, not an arbitrary fixture value).
        var redacted = new ReleaseSecret(secret).ToString();
        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
    }

    /// <summary>Publishes a real, not-expired <c>GiftListCreatedV1</c> plus one <c>GiftItemAddedV1</c>, and waits for this service's own projection to reflect both. Mirrors <c>ReserveGiftTests.CreateReservableListAsync</c>.</summary>
    private async Task<(Guid ListId, Guid ItemId)> CreateReservableListAsync()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddDays(7), ShareTokenPlaceholder, DateTimeOffset.UtcNow));
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(
            listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
        await WaitForItemOnProjectionAsync(listId, itemId);
        return (listId, itemId);
    }

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
