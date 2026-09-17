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
}
