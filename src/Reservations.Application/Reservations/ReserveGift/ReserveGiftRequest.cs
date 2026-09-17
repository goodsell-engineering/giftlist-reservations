namespace Reservations.Application.Reservations.ReserveGift;

/// <summary>
/// The Infrastructure-translated shape of <c>Reservations.Contracts.Reservations.ReserveGift</c>
/// (CONVENTIONS.md "Project reference graph" — Application never sees a Contracts type directly;
/// a Rebus handler in Infrastructure does that translation). This service mints its own
/// <c>ReservationId</c> and <c>ReleaseSecret</c>, so the wire command carries only the two ids a
/// caller actually supplies.
/// </summary>
public sealed record ReserveGiftRequest(Guid ListId, Guid ItemId);
