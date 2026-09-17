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
/// Carries no reserver identity of any kind (ARCHITECTURE.md "Reservation privacy") — the fields
/// below are the whole document.
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
}
