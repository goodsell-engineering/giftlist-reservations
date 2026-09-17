using Reservations.Application.GiftLists;
using Reservations.Application.Reservations.ReserveGift;
using Reservations.Domain.Reservations;
using Reservations.UnitTests.Support;

namespace Reservations.UnitTests.Reservations.ReserveGift;

/// <summary>
/// <c>ReserveGift</c> is a request/reply command (ARCHITECTURE.md "Command → event flow"), so
/// Reservations.IntegrationTests' <c>ReserveGiftTests</c> gets the success/failure shape for free
/// through the real bridge (CONVENTIONS.md "Testing") — this suite exists for the eligibility
/// branches that are fiddly to provoke deterministically through a real broker and real Mongo
/// (an already-expired projection, a tombstoned item, ordering between the save and the publish),
/// mirroring <c>GiftLists.UnitTests.GiftLists.CreateGiftList.CreateGiftListInteractorTests</c>'
/// own rationale exactly.
/// </summary>
public sealed class ReserveGiftInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ShouldReserveAndReturnTheReleaseSecret_WhenTheListExistsIsNotExpiredAndHasTheItem()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now.AddDays(7), IsDeleted: false, [new GiftItemId(itemId)]));
        var reservations = new FakeReservationRepository();
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, reservations, publisher, new FakeReleaseSecretGenerator("s3cr3tvaluewith32charactersabcde"), new FakeClock(Now));
        var request = new ReserveGiftRequest(listId, itemId);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("s3cr3tvaluewith32charactersabcde", result.Value.ReleaseSecret);
        Assert.Equal(Now, result.Value.ReservedAt);
        Assert.Equal([nameof(FakeReservationRepository.AddAsync)], reservations.Calls);
        Assert.Single(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldSaveTheReservation_BeforePublishingTheDomainEvents()
    {
        // Arrange — ARCHITECTURE.md "Event publishing: synchronous": save first, publish second.
        // One CallLog shared by both fakes is what makes cross-port ordering observable — see
        // CreateGiftListInteractorTests' own equivalent test for why.
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now.AddDays(7), IsDeleted: false, [new GiftItemId(itemId)]));
        var callLog = new CallLog();
        var reservations = new FakeReservationRepository { SharedLog = callLog };
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new ReserveGiftInteractor(
            giftLists, reservations, publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(listId, itemId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(
            [nameof(FakeReservationRepository.AddAsync), nameof(FakeDomainEventPublisher.PublishAsync)],
            callLog.Entries);
    }

    [Fact]
    public async Task Handle_ShouldReturnGiftListNotFound_WhenTheProjectionHasNoRowForTheList()
    {
        // Arrange — no Seed call: the projection has never heard of this list, which is
        // indistinguishable from "no such list" (IGiftListProjectionRepository.FindByIdAsync's
        // own doc comment).
        var giftLists = new FakeGiftListProjectionRepository();
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, new FakeReservationRepository(), publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_not_found", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnGiftListDeleted_WhenTheProjectionIsMarkedDeleted()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now.AddDays(7), IsDeleted: true, [new GiftItemId(itemId)]));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, new FakeReservationRepository(), publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(listId, itemId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_deleted", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnGiftListExpired_WhenExpiresAtIsAtOrBeforeNow()
    {
        // Arrange — ARCHITECTURE.md "Auth & sharing": expiry gates RESERVING, not viewing (Ryan's
        // decision, 2026-09-16), compared against the interactor's own clock. Exactly-at-Now, not
        // strictly before it — the boundary itself must already be treated as expired.
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now, IsDeleted: false, [new GiftItemId(itemId)]));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, new FakeReservationRepository(), publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(listId, itemId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftlist_expired", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnGiftItemNotFound_WhenTheItemIsNotOnTheProjection()
    {
        // Arrange — the list is otherwise perfectly reservable; only the item id is wrong, e.g. a
        // stale link or a since-removed item's tombstone.
        var listId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now.AddDays(7), IsDeleted: false, []));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, new FakeReservationRepository(), publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(listId, Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.giftitem_not_found", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheRepositoryFailure_AndPublishNothing_WhenAddAsyncFails()
    {
        // Arrange — a concurrent winner already took this (listId, itemId) pair
        // (ReservationErrors.AlreadyReserved); the unique index, not this interactor, is the
        // source of truth (GL-35).
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var giftLists = new FakeGiftListProjectionRepository();
        giftLists.Seed(new GiftListProjection(new GiftListId(listId), Now.AddDays(7), IsDeleted: false, [new GiftItemId(itemId)]));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ReserveGiftInteractor(
            giftLists, new FailingReservationRepository(), publisher, new FakeReleaseSecretGenerator(), new FakeClock(Now));

        // Act
        var result = await interactor.Handle(new ReserveGiftRequest(listId, itemId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("reservation.already_reserved", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }
}
