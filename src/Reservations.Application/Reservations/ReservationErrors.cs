using BuildingBlocks.Results;

namespace Reservations.Application.Reservations;

/// <summary>
/// Stable, machine-readable error codes for the Reservations use cases (CONVENTIONS.md "Errors"),
/// all <c>reservation.&lt;code&gt;</c> — two segments, the singular service name (CONVENTIONS.md
/// "Persistence" — database/queue names are singular even though the service directory is
/// plural), applied uniformly.
/// </summary>
public static class ReservationErrors
{
    /// <summary>
    /// The safety net behind <see cref="IReservationRepository.AddAsync"/>'s unique-index write on
    /// (listId, itemId) — see that member's own doc comment. This is the correctness mechanism
    /// behind "first reserver wins" (GL-35): the index, not whichever writer happens to observe
    /// the reply first, is what decides which insert wins a race.
    /// </summary>
    public static readonly Error AlreadyReserved = new(
        "reservation.already_reserved",
        "This gift has already been reserved.",
        ErrorKind.Conflict);

    /// <summary>
    /// <c>ReserveGift</c>'s own eligibility checks against the gift-list projection
    /// (<c>Reservations.Application.GiftLists.IGiftListProjectionRepository.FindByIdAsync</c>) —
    /// GL-36. Its own doc comment is explicit that a <see langword="null"/> projection means this
    /// service has never seen a <c>GiftListCreatedV1</c> for the id at all, which is
    /// indistinguishable from "no such list" from a reserving caller's point of view.
    /// </summary>
    public static readonly Error GiftListNotFound = new(
        "reservation.giftlist_not_found",
        "No such gift list exists.",
        ErrorKind.NotFound);

    /// <summary>A list this service once knew about, but GiftLists has since deleted — <see cref="GiftListNotFound"/>'s sibling for a known-but-gone list.</summary>
    public static readonly Error GiftListDeleted = new(
        "reservation.giftlist_deleted",
        "This gift list no longer exists.",
        ErrorKind.NotFound);

    /// <summary>
    /// Expiry gates RESERVING, not viewing (Ryan's decision, 2026-09-16) — this check compares
    /// the projection's own <c>ExpiresAt</c> against the interactor's own clock at the moment it
    /// matters, rather than waiting for a delayed <c>GiftListExpired</c> to arrive (ARCHITECTURE.md
    /// "Auth & sharing"). <see cref="ErrorKind.Conflict"/>, not <see cref="ErrorKind.NotFound"/>:
    /// the list is not gone, its current state just forbids this action — the same flavour as
    /// <see cref="AlreadyReserved"/>.
    /// </summary>
    public static readonly Error GiftListExpired = new(
        "reservation.giftlist_expired",
        "This gift list has expired and can no longer accept reservations.",
        ErrorKind.Conflict);

    /// <summary>The gift item id in a <c>ReserveGift</c> request does not (or no longer) appear on the list's own projection.</summary>
    public static readonly Error GiftItemNotFound = new(
        "reservation.giftitem_not_found",
        "No such gift item exists on this list.",
        ErrorKind.NotFound);
}
