namespace Reservations.Application.GiftLists.RecordGiftListCreated;

/// <summary>
/// The Infrastructure-translated shape of
/// <c>GiftLists.Contracts.GiftLists.Events.GiftListCreatedV1</c> (CONVENTIONS.md "Project
/// reference graph" — Application never sees a Contracts type directly; a Rebus handler in
/// Infrastructure does that translation, ARCHITECTURE.md "Consuming other services' events:
/// anti-corruption layer"). Carries only <see cref="ListId"/> and <see cref="ExpiresAt"/> — the
/// two fields this projection's reservability check needs — not every field the wire event
/// happens to carry (<c>OwnerId</c>/<c>Name</c>/<c>ShareToken</c>/<c>CreatedAt</c> have no reader
/// here). Delivery is at-least-once and not guaranteed in order, so this is safe to apply more
/// than once (CONVENTIONS.md "Messaging").
/// </summary>
public sealed record RecordGiftListCreatedRequest(Guid ListId, DateTimeOffset ExpiresAt);
