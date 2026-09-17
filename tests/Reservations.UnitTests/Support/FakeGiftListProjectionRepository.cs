using Reservations.Application.GiftLists;
using Reservations.Application.GiftLists.RecordGiftItemAdded;
using Reservations.Application.GiftLists.RecordGiftItemRemoved;
using Reservations.Application.GiftLists.RecordGiftListCreated;
using Reservations.Application.GiftLists.RecordGiftListDeleted;
using Reservations.Domain.Reservations;

namespace Reservations.UnitTests.Support;

/// <summary>
/// Hand-written in-memory fake, read side only — <c>ReserveGiftInteractorTests</c> is the only
/// consumer, and <c>ReserveGiftInteractor</c> only ever calls <see cref="FindByIdAsync"/> (one
/// aggregate mutated per interactor, CONVENTIONS.md "Use cases" — the projection is a different
/// set of interactors' concern entirely, GL-34). The four <c>Apply*</c> methods below exist only
/// to satisfy the interface and are never expected to be called from this suite.
/// </summary>
internal sealed class FakeGiftListProjectionRepository : IGiftListProjectionRepository
{
    private readonly Dictionary<Guid, GiftListProjection> _projections = [];

    /// <summary>Puts a projection directly into the fake's store — test setup, not something <c>ReserveGiftInteractor</c> itself would ever cause.</summary>
    public void Seed(GiftListProjection projection) => _projections[projection.ListId.Value] = projection;

    public Task<GiftListProjection?> FindByIdAsync(GiftListId listId, CancellationToken cancellationToken)
    {
        _projections.TryGetValue(listId.Value, out var found);
        return Task.FromResult(found);
    }

    public Task ApplyListCreatedAsync(RecordGiftListCreatedRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{nameof(FakeGiftListProjectionRepository)} is read-only.");

    public Task ApplyListDeletedAsync(RecordGiftListDeletedRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{nameof(FakeGiftListProjectionRepository)} is read-only.");

    public Task ApplyItemAddedAsync(RecordGiftItemAddedRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{nameof(FakeGiftListProjectionRepository)} is read-only.");

    public Task ApplyItemRemovedAsync(RecordGiftItemRemovedRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{nameof(FakeGiftListProjectionRepository)} is read-only.");
}
