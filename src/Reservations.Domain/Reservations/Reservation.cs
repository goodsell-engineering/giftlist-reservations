using Reservations.Domain.Common;

namespace Reservations.Domain.Reservations;

/// <summary>
/// The Reservation service's one aggregate root. A single fact — "this gift item, on this gift
/// list, is reserved" — and nothing else: ARCHITECTURE.md "Reservation privacy" makes the absence
/// of any further field a product guarantee, not an oversight. No reserver identity is ever
/// collected here — not a name, not an email, not a session id, not an IP — so there is
/// structurally nothing on this type that could correlate two reservations to one person.
/// </summary>
/// <remarks>
/// GL-35's scope is this aggregate's persistence shape and the unique index behind "first
/// reserver wins" (<c>ReservationRepository.EnsureIndexesAsync</c>) — not the <c>ReserveGift</c>
/// use case itself (GL-36), so this type raises no domain event yet. GL-36 decides what
/// <c>GiftReserved</c> looks like and how it is raised when it builds the interactor that
/// actually calls <see cref="Create"/>.
/// </remarks>
public sealed class Reservation
{
    private Reservation(ReservationId id, GiftListId listId, GiftItemId itemId, DateTimeOffset reservedAt)
    {
        Id = id;
        ListId = listId;
        ItemId = itemId;
        // Truncated to what Mongo can round-trip — see Timestamps.ToStoredPrecision.
        ReservedAt = Timestamps.ToStoredPrecision(reservedAt);
    }

    public ReservationId Id { get; }

    public GiftListId ListId { get; }

    public GiftItemId ItemId { get; }

    public DateTimeOffset ReservedAt { get; }

    /// <summary>
    /// Creates a brand-new reservation. Whether this particular (<paramref name="listId"/>,
    /// <paramref name="itemId"/>) pair is actually the first — "first reserver wins" — is not
    /// decided here: it is enforced by the unique compound index on those two fields
    /// (<c>ReservationRepository.EnsureIndexesAsync</c>), which the repository's insert either
    /// succeeds or fails against. This factory has nothing to check; it exists so
    /// construction goes through one named place rather than <c>new Reservation(...)</c>
    /// appearing wherever one is needed, and so <see cref="Rehydrate"/> stays the only other way
    /// to obtain one (CONVENTIONS.md "Domain modelling" — no public parameterless constructor).
    /// </summary>
    public static Reservation Create(ReservationId id, GiftListId listId, GiftItemId itemId, DateTimeOffset reservedAt) =>
        new(id, listId, itemId, reservedAt);

    /// <summary>
    /// Rebuilds a <see cref="Reservation"/> from persisted state. Called only by the
    /// Infrastructure mapper (CONVENTIONS.md "Domain modelling").
    /// </summary>
    public static Reservation Rehydrate(ReservationId id, GiftListId listId, GiftItemId itemId, DateTimeOffset reservedAt) =>
        new(id, listId, itemId, reservedAt);
}
