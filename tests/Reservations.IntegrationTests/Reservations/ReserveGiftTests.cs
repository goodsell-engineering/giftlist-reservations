using GiftLists.Contracts.GiftLists.Events;
using Reservations.Application.GiftLists;
using Reservations.Contracts.Reservations;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// Enters at Reservations' real entry point — the <c>ReserveGiftHandler</c> Rebus handler,
/// reached by sending the wire <see cref="ReserveGift"/> command through the real request/reply
/// bridge exactly the way the Gateway would (ARCHITECTURE.md "Command → event flow", the same
/// bridge Identity's Login/SignUp reuse). All infrastructure is real (CONVENTIONS.md "Testing"):
/// the gift-list projection this suite reserves against is itself built from real
/// <c>GiftLists.Contracts</c> events published onto the real broker (GL-34's own handlers), not a
/// fixture shortcut.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class ReserveGiftTests(ReservationsFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// <c>GiftListCreatedV1.ShareToken</c> is not one of the fields
    /// <c>RecordGiftListCreatedRequest</c> even carries (<c>GiftListCreatedV1Handler</c> reads
    /// only <c>ListId</c>/<c>ExpiresAt</c> — this projection never displays a list, so it has no
    /// use for a share token at all), so any well-formed string here is as good as a real one.
    /// </summary>
    private const string ShareTokenPlaceholder = "ShareTokenPlaceholder";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReserveGift_ShouldSucceedAndReturnAReleaseSecret_WhenTheItemIsReservable()
    {
        // Arrange
        var (listId, itemId) = await CreateReservableListAsync();

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ReleaseSecret.Length, result.Value.ReleaseSecret.Length);
    }

    [Fact]
    public async Task ReserveGift_ShouldReturnAlreadyReserved_WhenTheSameItemIsReservedTwice()
    {
        // Arrange — CONVENTIONS.md "Testing": only a real Mongo collection enforces the unique
        // (listId, itemId) index (GL-35) that makes this the actual source of truth, not merely a
        // pre-check.
        var (listId, itemId) = await CreateReservableListAsync();
        var first = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));
        Assert.True(first.IsSuccess);

        // Act
        var second = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));

        // Assert
        Assert.True(second.IsFailure);
        Assert.Equal("reservation.already_reserved", second.Error.Code);
    }

    [Fact]
    public async Task ReserveGift_ShouldReturnGiftListNotFound_WhenNoSuchListExists()
    {
        // Arrange — this service has never heard of this list at all, which is
        // indistinguishable from "no such list" (IGiftListProjectionRepository.FindByIdAsync's
        // own doc comment).
        var unknownListId = Guid.NewGuid();

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(
            new ReserveGift(unknownListId, Guid.NewGuid()));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ReserveGift_ShouldReturnGiftListDeleted_WhenTheListHasBeenDeleted()
    {
        // Arrange
        var (listId, itemId) = await CreateReservableListAsync();
        await fixture.GiftListsBus.Publish(new GiftListDeletedV1(listId, DateTimeOffset.UtcNow));
        await WaitForProjectionAsync(listId, projection => projection?.IsDeleted == true);

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_deleted", result.Error.Code);
    }

    [Fact]
    public async Task ReserveGift_ShouldReturnGiftListExpired_WhenTheListsExpiresAtIsInThePast()
    {
        // Arrange — ARCHITECTURE.md "Auth & sharing": expiry gates RESERVING, not viewing (Ryan's
        // decision, 2026-09-16). A list published already-expired, not one waited out in real
        // time — this suite has no reason to run any slower than it has to.
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddSeconds(-1), ShareTokenPlaceholder, DateTimeOffset.UtcNow));
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(
            listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
        await WaitForProjectionAsync(listId, projection => projection?.ItemIds.Contains(new GiftItemId(itemId)) == true);

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(new ReserveGift(listId, itemId));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_expired", result.Error.Code);
    }

    /// <summary>
    /// GL-37: proves <c>Validating&lt;,&gt;</c> (CONVENTIONS.md "Use cases") actually wraps
    /// <c>ReserveGift</c>'s interactor over the real wire — the request/reply bridge, the Rebus
    /// handler, the composition root's decorator pipeline, all real — not merely that
    /// <c>ReserveGiftValidator</c> rejects <see cref="Guid.Empty"/> in isolation
    /// (<c>ReserveGiftInteractorTests</c>/a hand-written validator unit test could both stay green
    /// with the decorator never registered at all).
    /// </summary>
    [Fact]
    public async Task ReserveGift_ShouldReturnInvalidId_WhenTheListIdIsEmpty()
    {
        // Arrange — none; Guid.Empty is invalid regardless of whether the item id or anything
        // else about the request is otherwise well-formed.

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(
            new ReserveGift(Guid.Empty, Guid.NewGuid()));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.invalid_id", result.Error.Code);
    }

    [Fact]
    public async Task ReserveGift_ShouldReturnGiftItemNotFound_WhenTheItemIsUnknown()
    {
        // Arrange — the list itself is perfectly reservable; only the item id is wrong.
        var (listId, _) = await CreateReservableListAsync();

        // Act
        var result = await fixture.RequestReplyBridge.SendAndAwaitReply<ReserveGiftReply>(
            new ReserveGift(listId, Guid.NewGuid()));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftitem_not_found", result.Error.Code);
    }

    /// <summary>Publishes a real, not-expired <c>GiftListCreatedV1</c> plus one <c>GiftItemAddedV1</c>, and waits for this service's own projection to reflect both.</summary>
    private async Task<(Guid ListId, Guid ItemId)> CreateReservableListAsync()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await fixture.GiftListsBus.Publish(new GiftListCreatedV1(
            listId, Guid.NewGuid(), "Birthday Wishlist", DateTimeOffset.UtcNow.AddDays(7), ShareTokenPlaceholder, DateTimeOffset.UtcNow));
        await fixture.GiftListsBus.Publish(new GiftItemAddedV1(
            listId, itemId, "Lego Set", null, null, DateTimeOffset.UtcNow));
        await WaitForProjectionAsync(listId, projection => projection?.ItemIds.Contains(new GiftItemId(itemId)) == true);
        return (listId, itemId);
    }

    private async Task WaitForProjectionAsync(Guid listId, Func<GiftListProjection?, bool> isReady)
    {
        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListProjectionRepository>();
        await Eventually.Async(
            () => repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None),
            isReady,
            WaitTimeout);
    }
}
