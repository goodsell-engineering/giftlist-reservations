using Reservations.Contracts.Reservations.Events;
using Reservations.Domain.Common;
using Reservations.Domain.Reservations.Events;

namespace Reservations.Infrastructure.Reservations.Messaging;

/// <summary>
/// Translates domain events into the integration events Reservation actually publishes
/// (ARCHITECTURE.md "Domain events are not integration events" — and they never leave the process
/// directly). The one-to-one shape looks like ceremony with a single domain event today; it is
/// the seam that lets the domain change without a wire-breaking change tomorrow, mirroring
/// <c>Identity.Infrastructure.Users.Messaging.UserEventMapper</c>/
/// <c>GiftLists.Infrastructure.GiftLists.Messaging.GiftListEventMapper</c> exactly.
/// </summary>
internal static class ReservationEventMapper
{
    public static object ToIntegrationEvent(IDomainEvent domainEvent) => domainEvent switch
    {
        GiftReserved e => new GiftReservedV1(e.ListId.Value, e.ItemId.Value, e.ReservedAt),
        _ => throw new InvalidOperationException(
            $"No integration event mapping for domain event '{domainEvent.GetType().Name}'."),
    };

    /// <summary>
    /// The list id a domain event concerns, for diagnostics. Lives here, beside the exhaustive
    /// mapping switch, so per-event-type knowledge stays in one place rather than being
    /// re-derived by a caller that only needs one field (mirrors Identity's
    /// <c>UserEventMapper.UserIdOf</c>/GiftLists' <c>ListIdOf</c>, GL-62). Non-throwing by design,
    /// for the same reason as those: it is called from <see cref="ReservationEventPublisher"/>'s
    /// catch block, and it must never be the thing that throws there. Deliberately the list id,
    /// not the reservation id — the list id is what a reader would use to correlate this log line
    /// with the gift list the drift concerns, and it carries no reserver identity either way
    /// (ARCHITECTURE.md "Reservation privacy").
    /// </summary>
    public static Guid ListIdOf(IDomainEvent domainEvent) => domainEvent switch
    {
        GiftReserved e => e.ListId.Value,
        _ => Guid.Empty,
    };
}
