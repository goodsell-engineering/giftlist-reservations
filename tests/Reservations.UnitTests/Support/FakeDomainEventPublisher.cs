using Reservations.Application.Common;
using Reservations.Domain.Common;

namespace Reservations.UnitTests.Support;

/// <summary>Hand-written fake — records every batch of domain events an interactor publishes, so a test can assert what (and in what order relative to the repository call) was published without a real broker.</summary>
internal sealed class FakeDomainEventPublisher : IDomainEventPublisher
{
    public List<IReadOnlyCollection<IDomainEvent>> PublishedBatches { get; } = [];

    /// <summary>Set to the SAME <see cref="CallLog"/> as the repository fake to assert cross-port call order.</summary>
    public CallLog? SharedLog { get; init; }

    public Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        SharedLog?.Record(nameof(PublishAsync));
        PublishedBatches.Add(domainEvents);
        return Task.CompletedTask;
    }
}
