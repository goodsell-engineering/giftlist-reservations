using Reservations.Application.GiftLists;
using Reservations.Domain.Reservations;

namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>Source + "To" + target (CONVENTIONS.md "Naming").</summary>
internal static class GiftListProjectionDocumentMapper
{
    /// <summary>
    /// Removed items (tombstones — see <see cref="GiftItemProjectionDocument.IsRemoved"/>'s own
    /// doc comment) are filtered out here, never on the way in: this is the one place that
    /// decides what a query response looks like, so it is the one place a future second query
    /// cannot forget the filter. Wraps the document's bare <see cref="Guid"/>s in the same
    /// strongly-typed <see cref="GiftListId"/>/<see cref="GiftItemId"/> the <c>Reservation</c>
    /// aggregate itself uses (CONVENTIONS.md "Domain modelling") — this document/mapper pair is
    /// the one seam that ever converts between the two.
    /// </summary>
    public static GiftListProjection ToProjection(GiftListProjectionDocument document) => new(
        new GiftListId(document.Id),
        new DateTimeOffset(document.ExpiresAt, TimeSpan.Zero),
        document.IsDeleted,
        document.Items
            .Where(item => !item.IsRemoved)
            .Select(item => new GiftItemId(item.ItemId))
            .ToList());
}
