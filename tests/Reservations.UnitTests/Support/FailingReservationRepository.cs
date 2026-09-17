using BuildingBlocks.Results;
using Reservations.Application.Reservations;
using Reservations.Domain.Reservations;

namespace Reservations.UnitTests.Support;

/// <summary>Hand-written fake whose <see cref="AddAsync"/> always reports the unique-index collision a real Mongo write would (<see cref="ReservationErrors.AlreadyReserved"/>'s own doc comment) — the write-failure path <see cref="FakeReservationRepository"/> cannot itself provoke.</summary>
internal sealed class FailingReservationRepository : IReservationRepository
{
    public Task<Reservation?> FindByIdAsync(ReservationId id, CancellationToken cancellationToken) =>
        Task.FromResult<Reservation?>(null);

    public Task<Result> AddAsync(Reservation reservation, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(ReservationErrors.AlreadyReserved));
}
