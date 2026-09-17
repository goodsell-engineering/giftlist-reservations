namespace Reservations.Application.Reservations.ReserveGift;

/// <summary>
/// <see cref="ReleaseSecret"/> is returned here ONLY — never queried, projected or published
/// (ARCHITECTURE.md "Data model"; ARCHITECTURE.md "Reservation privacy"). The Infrastructure
/// Rebus handler maps this 1:1 onto <c>Reservations.Contracts.Reservations.ReserveGiftReply</c>,
/// the wire reply the request/reply bridge hands back to the one browser that just reserved this
/// item — GL-37 is what guarantees that delivery is browser-scoped; this response only needs to
/// get the value as far as that handler.
/// </summary>
public sealed record ReserveGiftResponse(string ReleaseSecret, DateTimeOffset ReservedAt);
