namespace Reservations.Contracts.Reservations.Events;

/// <summary>
/// Published after a gift item is successfully reserved (ARCHITECTURE.md "Event catalogue":
/// Reservation → Gateway). The wire counterpart of the domain event
/// <c>Reservations.Domain.Reservations.Events.GiftReserved</c> — mapped by an Infrastructure event
/// mapper, never published directly (ARCHITECTURE.md "Domain events are not integration events").
///
/// Carries <see cref="ListId"/>, <see cref="ItemId"/> and <see cref="ReservedAt"/> ONLY —
/// deliberately no reservation id, and deliberately no <c>releaseSecret</c>. ARCHITECTURE.md
/// "Reservation privacy" is a hard guarantee enforced by the absence of data: "nothing
/// identifying is stored, published in GiftReserved, or projected into the read model" — no name,
/// no email, no session id, no IP, nothing that could correlate two reservations to one person.
/// A field added here cannot be removed again without a major version bump (the
/// giftlist-contract-change procedure), so the guarantee is cheapest to keep by never adding one.
/// </summary>
public sealed record GiftReservedV1(Guid ListId, Guid ItemId, DateTimeOffset ReservedAt);
