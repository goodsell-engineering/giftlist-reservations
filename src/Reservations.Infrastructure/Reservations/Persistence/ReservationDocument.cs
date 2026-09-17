namespace Reservations.Infrastructure.Reservations.Persistence;

/// <summary>
/// The Mongo-facing shape of a reservation (ARCHITECTURE.md "Data model" —
/// <c>reservation.reservations</c>), kept separate from the
/// <c>Reservations.Domain.Reservations.Reservation</c> aggregate (ARCHITECTURE.md "Data that
/// crosses boundaries" — no <c>[Bson*]</c> attributes in Domain). No attributes are needed here
/// either: <see cref="Id"/> auto-maps to <c>_id</c> by the driver's own default convention, and
/// field names are camelCased by the shared <c>BuildingBlocks.Persistence.MongoConventions</c>
/// pack registered once at startup.
///
/// <see cref="ReservedAt"/> is <see cref="DateTime"/>, not the aggregate's own
/// <see cref="DateTimeOffset"/> — see
/// <c>Identity.Infrastructure.Users.Persistence.UserDocument.CreatedAt</c>'s own doc comment for
/// why (the driver's default <see cref="DateTimeOffset"/> representation doesn't range-query
/// like a native BSON date). It is always UTC (sourced from <c>IClock.UtcNow</c>), so nothing is
/// lost by storing it as one.
///
/// Carries no reserver identity of any kind (ARCHITECTURE.md "Reservation privacy") — the fields
/// below are the whole document. <see cref="ReleaseSecret"/> (ARCHITECTURE.md "Data model") is
/// the one opaque, unguessable capability stored here, minted once by <c>ReserveGift</c> (GL-36)
/// and returned to a client only once, in its reply — never queried, projected or published from
/// here again.
/// </summary>
public sealed class ReservationDocument
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Paired with <see cref="ItemId"/> by the unique compound index
    /// <c>ReservationRepository.EnsureIndexesAsync</c> declares — the correctness mechanism
    /// behind "first reserver wins" (GL-35).
    /// </summary>
    public required Guid ListId { get; init; }

    public required Guid ItemId { get; init; }

    public required DateTime ReservedAt { get; init; }

    /// <summary>
    /// The <c>Reservations.Domain.Reservations.ReleaseSecret</c>'s raw string (GL-36). Never
    /// exposed by any query response, projection, or event (ARCHITECTURE.md "Reservation privacy");
    /// it is returned to a caller exactly once, in the <c>ReserveGift</c> reply, at the
    /// moment it is minted.
    /// </summary>
    public required string ReleaseSecret { get; init; }
}
