using Reservations.Application.Common;
using Reservations.Domain.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Reservations.Infrastructure.Reservations.Messaging;

/// <summary>
/// The Reservations-specific implementation of the generic <see cref="IDomainEventPublisher"/>
/// (ARCHITECTURE.md "Domain events are not integration events"): maps each domain event to its
/// integration event and publishes it. Separate from <c>ReservationRepository</c> on purpose —
/// see <see cref="IDomainEventPublisher"/>'s own doc comment.
/// </summary>
/// <remarks>
/// Publishing is synchronous, straight after the Mongo write, with no outbox — a known, accepted
/// dual-write risk (ARCHITECTURE.md "Event publishing: synchronous"): if the write succeeds and
/// this then fails, the Gateway's read model drifts from Reservation's own data with no automatic
/// repair. Given that risk is already accepted, rethrowing from here would only make things
/// worse — for the fire-and-forget commands this would be true of every other use case here, but
/// <c>ReserveGift</c> is a request/reply command with a caller already waiting on
/// <see cref="ReserveGiftHandler"/>'s own <c>bus.Reply</c>; that reply has already been decided by
/// the time this runs (save first, publish second), so rethrowing here would not even reach the
/// waiting caller — it would just land an already-applied write in Rebus's error queue for a
/// retry that re-runs a mutation which already happened. Instead this swallows the failure and
/// logs it at <see cref="LogLevel.Critical"/>, deliberately loud, so the drift is a page an
/// operator sees rather than a silent gap discovered weeks later. Recovery is manual
/// (restart/replay), exactly as ARCHITECTURE.md "Event publishing: synchronous" anticipates.
/// Mirrors Identity's <c>UserEventPublisher</c>/GiftLists' <c>GiftListEventPublisher</c>
/// deliberately (GL-62) — one behaviour, not a third phrasing of it.
/// </remarks>
internal sealed class ReservationEventPublisher(IBus bus, ILogger<ReservationEventPublisher> logger) : IDomainEventPublisher
{
    public async Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        // The try wraps the WHOLE loop, not each iteration — see UserEventPublisher's own doc
        // comment for why a partial, holed sequence is worse than a contiguous gap.
        var published = 0;

        try
        {
            foreach (var domainEvent in domainEvents)
            {
                await bus.Publish(ReservationEventMapper.ToIntegrationEvent(domainEvent));
                published++;
            }
        }
        catch (Exception ex)
        {
            // NOTHING IN THIS BLOCK MAY CALL ToIntegrationEvent — see UserEventPublisher's own
            // doc comment for the exact bug this avoids repeating (GiftLists' Batch 11 review):
            // if the caught exception WAS the mapper's own default-arm throw, re-invoking it here
            // would repeat the same throw and defeat the Critical log entirely. The V1 type name
            // is derivable from the domain event type name, so logging the latter loses nothing;
            // ListIdOf is non-throwing by design for the same reason.
            var failed = domainEvents.ElementAt(published);

            logger.LogCritical(
                ex,
                "Failed to publish the integration event for domain event {DomainEventType} for " +
                "gift list {ListId}; {PublishedCount} of {TotalCount} event(s) in this batch were " +
                "published and the rest were abandoned. The write they describe already succeeded, so " +
                "Reservation's read-model consumers (Gateway) are now missing them until this is manually " +
                "reconciled (ARCHITECTURE.md \"Event publishing: synchronous\", dual-write risk accepted knowingly). The list id is " +
                "here because it is the only way to know WHICH list drifted — this log is the sole " +
                "record of that, and it carries no reserver identity either way (ARCHITECTURE.md " +
                "\"Reservation privacy\").",
                failed.GetType().Name,
                ReservationEventMapper.ListIdOf(failed),
                published,
                domainEvents.Count);
        }
    }
}
