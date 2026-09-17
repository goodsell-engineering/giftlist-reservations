namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>
/// The embedded, per-item shape inside <see cref="GiftListProjectionDocument.Items"/> — whether one
/// gift item still exists on its list, nothing else (no name, description or url: this service
/// never displays an item, only checks whether it may still be reserved).
///
/// A removed item is never deleted from this array — <see cref="IsRemoved"/> plus
/// <see cref="UpdatedAt"/> is a tombstone, mirroring
/// <c>Gateway.Infrastructure.GiftLists.Persistence.GiftItemProjectionDocument</c>'s own doc
/// comment exactly: the hazard this exists for is a <c>GiftItemRemovedV1</c> being processed
/// before its matching <c>GiftItemAddedV1</c> for the same <see cref="ItemId"/> (CONVENTIONS.md
/// "Messaging" — at-least-once, out-of-order delivery). If removal deleted the array entry
/// outright, that add would later arrive to an empty slot and re-add an item that was already
/// (chronologically) removed. Keeping the tombstone means the add's own last-write-wins guard —
/// <see cref="UpdatedAt"/> already newer than the add's own timestamp — rejects it instead.
/// <see cref="GiftListProjectionDocumentMapper.ToProjection"/> filters every
/// <see cref="IsRemoved"/> item out before this ever reaches
/// <see cref="Application.GiftLists.GiftListProjection.ItemIds"/>.
/// </summary>
public sealed class GiftItemProjectionDocument
{
    public required Guid ItemId { get; init; }

    public required bool IsRemoved { get; init; }

    /// <summary>
    /// The timestamp of whichever of <c>GiftItemAddedV1.AddedAt</c> / <c>GiftItemRemovedV1.RemovedAt</c>
    /// last legitimately applied to this item — the last-write-wins guard
    /// <see cref="GiftListProjectionRepository"/> compares an incoming event's own timestamp
    /// against before applying it. <see cref="DateTime"/>, not the wire contract's
    /// <see cref="DateTimeOffset"/> — see <see cref="ProjectionInstants"/>'s own doc comment.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
