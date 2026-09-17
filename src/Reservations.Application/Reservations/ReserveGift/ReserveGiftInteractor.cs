using BuildingBlocks.Results;
using Reservations.Application.Common;
using Reservations.Application.GiftLists;
using Reservations.Domain.Reservations;

namespace Reservations.Application.Reservations.ReserveGift;

/// <summary>
/// Assumes its request already passed <see cref="ReserveGiftValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// One aggregate mutated per interactor (CONVENTIONS.md "Use cases") — the gift-list projection
/// is only ever read here, never written; <see cref="IGiftListProjectionRepository"/>'s own
/// Apply* methods are the only writers, and they belong to a different set of interactors
/// entirely (GL-34).
/// </summary>
internal sealed class ReserveGiftInteractor(
    IGiftListProjectionRepository giftLists,
    IReservationRepository reservations,
    IDomainEventPublisher reservationEvents,
    IReleaseSecretGenerator releaseSecrets,
    IClock clock) : IReserveGift
{
    public async Task<Result<ReserveGiftResponse>> Handle(ReserveGiftRequest request, CancellationToken cancellationToken)
    {
        var listId = new GiftListId(request.ListId);
        var itemId = new GiftItemId(request.ItemId);

        // Do not reserve against a list the projection does not know (Ryan's decision,
        // 2026-09-16) — a null projection means either "no such list" or "this service has not
        // caught up with a GiftListCreatedV1 yet", and the caller cannot and need not tell those
        // apart (IGiftListProjectionRepository.FindByIdAsync's own doc comment).
        var projection = await giftLists.FindByIdAsync(listId, cancellationToken);
        if (projection is null)
        {
            return ReservationErrors.GiftListNotFound;
        }

        if (projection.IsDeleted)
        {
            return ReservationErrors.GiftListDeleted;
        }

        // Expiry gates RESERVING, not viewing (Ryan's decision, 2026-09-16): compared against
        // this interactor's own clock at the moment it matters, not a possibly-delayed
        // GiftListExpired event this service does not even subscribe to (GL-34's own rationale).
        var now = clock.UtcNow;
        if (projection.ExpiresAt <= now)
        {
            return ReservationErrors.GiftListExpired;
        }

        if (!projection.ItemIds.Contains(itemId))
        {
            return ReservationErrors.GiftItemNotFound;
        }

        var releaseSecret = new ReleaseSecret(releaseSecrets.Generate());
        var reservation = Reservation.Create(ReservationId.New(), listId, itemId, releaseSecret, now);

        // The eligibility checks above are a UX nicety, not the source of truth for "first
        // reserver wins" — the unique (listId, itemId) index is (GL-35; ARCHITECTURE.md "Data
        // model"). A concurrent winner surfaces here as ReservationErrors.AlreadyReserved, an
        // expected outcome returned as a value, never an exception (CONVENTIONS.md "Errors").
        var saved = await reservations.AddAsync(reservation, cancellationToken);
        if (saved.IsFailure)
        {
            return Result<ReserveGiftResponse>.Failure(saved.Error);
        }

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — a
        // published event describing a write that didn't happen is worse than a write nobody
        // heard about. See ReservationEventPublisher for how a publish failure is handled (logged
        // loudly, not thrown) so it never masks the write that already succeeded.
        await reservationEvents.PublishAsync(reservation.DomainEvents, cancellationToken);
        reservation.ClearDomainEvents();

        return new ReserveGiftResponse(reservation.ReleaseSecret.Value, reservation.ReservedAt);
    }
}
