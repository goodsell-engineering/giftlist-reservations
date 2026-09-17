using Reservations.Domain.Common;

namespace Reservations.Application.Common;

/// <summary>
/// Publishes an aggregate's saved domain events as integration events (ARCHITECTURE.md "Domain
/// events are not integration events"). Kept separate from a repository's <c>AddAsync</c>
/// deliberately — a repository that both writes and publishes conflates persistence with
/// messaging. Genuinely domain-agnostic in signature (it never mentions <c>Reservation</c>
/// specifically), so it lives in <c>Common/</c> next to <see cref="IClock"/>/
/// <see cref="IReleaseSecretGenerator"/> rather than beside
/// <c>Reservations.Application.Reservations.IReservationRepository</c> — mirrors
/// <c>Identity.Application.Common.IDomainEventPublisher</c>/
/// <c>GiftLists.Application.Common.IDomainEventPublisher</c> exactly. The interactor calls this
/// only after the repository's save has succeeded — save first, publish second (CONVENTIONS.md
/// "Messaging") — and the mapping from domain to integration event happens behind this port, in
/// Infrastructure, never in Application (ARCHITECTURE.md "Domain events are not integration
/// events").
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
