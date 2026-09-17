namespace Reservations.Application.GiftLists.RecordGiftItemAdded;

/// <summary>
/// Translated from <c>GiftLists.Contracts.GiftLists.Events.GiftItemAddedV1</c> (ARCHITECTURE.md
/// "Consuming other services' events: anti-corruption layer"). Carries only the item's existence
/// (<see cref="ListId"/>/<see cref="ItemId"/>/<see cref="AddedAt"/>) — <c>Name</c>/
/// <c>Description</c>/<c>Url</c> have no reader in this service, which never displays an item,
/// only checks whether one may still be reserved.
/// </summary>
public sealed record RecordGiftItemAddedRequest(Guid ListId, Guid ItemId, DateTimeOffset AddedAt);
