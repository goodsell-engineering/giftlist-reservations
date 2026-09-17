using Reservations.Domain.Common;
using Reservations.Domain.Reservations.Events;

namespace Reservations.Domain.Reservations;

/// <summary>
/// The Reservation service's one aggregate root. A single fact — "this gift item, on this gift
/// list, is reserved" — plus the one capability its own reserving browser needs to undo it
/// (<see cref="ReleaseSecret"/>). ARCHITECTURE.md "Reservation privacy" makes the absence of any
/// further field a product guarantee, not an oversight. No reserver identity is ever collected
/// here — not a name, not an email, not a session id, not an IP — so there is structurally
/// nothing on this type that could correlate two reservations to one person.
/// </summary>
/// <remarks>
/// GL-35 built this aggregate's persistence shape and the unique index behind "first reserver
/// wins" (<c>ReservationRepository.EnsureIndexesAsync</c>), deferring both <see cref="ReleaseSecret"/>
/// and the <c>ReserveGift</c> use case itself. GL-36 adds both: <see cref="Create"/> now mints a
/// <see cref="ReleaseSecret"/> and raises <see cref="GiftReserved"/>, and
/// <c>Reservations.Application.Reservations.ReserveGift.ReserveGiftInteractor</c> is what actually
/// calls it.
/// </remarks>
public sealed class Reservation
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Reservation(
        ReservationId id, GiftListId listId, GiftItemId itemId, ReleaseSecret releaseSecret, DateTimeOffset reservedAt)
    {
        Id = id;
        ListId = listId;
        ItemId = itemId;
        ReleaseSecret = releaseSecret;
        // Truncated to what Mongo can round-trip — see Timestamps.ToStoredPrecision.
        ReservedAt = Timestamps.ToStoredPrecision(reservedAt);
    }

    public ReservationId Id { get; }

    public GiftListId ListId { get; }

    public GiftItemId ItemId { get; }

    public ReleaseSecret ReleaseSecret { get; }

    public DateTimeOffset ReservedAt { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Creates a brand-new reservation, raising <see cref="GiftReserved"/>. Whether this
    /// particular (<paramref name="listId"/>, <paramref name="itemId"/>) pair is actually the
    /// first — "first reserver wins" — is not decided here: it is enforced by the unique compound
    /// index on those two fields (<c>ReservationRepository.EnsureIndexesAsync</c>), which the
    /// repository's insert either succeeds or fails against. The only path that raises a domain
    /// event — <see cref="Rehydrate"/> never does, since loading an existing reservation back out
    /// of storage is not something newly happening (CONVENTIONS.md "Domain modelling").
    /// </summary>
    public static Reservation Create(
        ReservationId id, GiftListId listId, GiftItemId itemId, ReleaseSecret releaseSecret, DateTimeOffset reservedAt)
    {
        var reservation = new Reservation(id, listId, itemId, releaseSecret, reservedAt);
        // reservation.ReservedAt, NOT the raw reservedAt parameter: the constructor normalised
        // it, and an event carrying the un-normalised value would tell downstream consumers
        // something that disagrees with what reservation.reservations holds (mirrors GL-63's fix
        // in GiftLists.Domain.GiftLists.GiftList.Create/Identity.Domain.Users.User.Register).
        reservation._domainEvents.Add(new GiftReserved(listId, itemId, reservation.ReservedAt));
        return reservation;
    }

    /// <summary>
    /// Rebuilds a <see cref="Reservation"/> from persisted state. Called only by the
    /// Infrastructure mapper (CONVENTIONS.md "Domain modelling" — no public parameterless
    /// constructor; rehydration goes through the mapper). Never raises domain events.
    /// </summary>
    public static Reservation Rehydrate(
        ReservationId id, GiftListId listId, GiftItemId itemId, ReleaseSecret releaseSecret, DateTimeOffset reservedAt) =>
        new(id, listId, itemId, releaseSecret, reservedAt);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
