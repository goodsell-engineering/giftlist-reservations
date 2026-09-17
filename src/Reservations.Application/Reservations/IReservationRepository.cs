using BuildingBlocks.Results;
using Reservations.Domain.Reservations;

namespace Reservations.Application.Reservations;

/// <summary>
/// Port lives beside the domain it serves (CONVENTIONS.md "Folder structure"), not in a shared
/// Abstractions bucket. One repository per aggregate root (CONVENTIONS.md "Persistence").
/// </summary>
public interface IReservationRepository
{
    Task<Reservation?> FindByIdAsync(ReservationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a brand-new reservation. Returns <see cref="ReservationErrors.AlreadyReserved"/>
    /// if the unique compound index on (listId, itemId) rejects the insert — GL-35's whole point:
    /// the index, not this port's caller, is what makes "first reserver wins" correct, because it
    /// is enforced by Mongo itself against every concurrent writer at once, not just against
    /// whichever ones a single process happened to see. An expected outcome is a value
    /// (CONVENTIONS.md "Errors"), not a thrown exception, even though the failure originates as a
    /// driver-level write error.
    /// </summary>
    Task<Result> AddAsync(Reservation reservation, CancellationToken cancellationToken);
}
