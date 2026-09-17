using BuildingBlocks.Results;
using Reservations.Application.Reservations;
using Reservations.Domain.Reservations;

namespace Reservations.UnitTests.Support;

/// <summary>
/// Hand-written in-memory fake (CONVENTIONS.md "Testing" — preferred over a mock for a port used
/// across many interactor tests). Only for <c>Reservations.UnitTests</c>: the IntegrationTests
/// suite bans in-memory repositories outright, since only a real Mongo collection enforces the
/// unique (listId, itemId) index.
/// </summary>
internal sealed class FakeReservationRepository : IReservationRepository
{
    private readonly Dictionary<Guid, Reservation> _reservations = [];

    /// <summary>Every call this fake received, in order — lets a test assert repository-then-publisher call ordering without a real broker.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Set to the SAME <see cref="CallLog"/> as the publisher fake to assert cross-port call order.</summary>
    public CallLog? SharedLog { get; init; }

    public Task<Reservation?> FindByIdAsync(ReservationId id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(FindByIdAsync));
        SharedLog?.Record(nameof(FindByIdAsync));
        _reservations.TryGetValue(id.Value, out var found);
        return Task.FromResult(found);
    }

    public Task<Result> AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddAsync));
        SharedLog?.Record(nameof(AddAsync));
        _reservations[reservation.Id.Value] = reservation;
        return Task.FromResult(Result.Success());
    }
}
