using Reservations.Application.GiftLists;

namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>Source + "To" + target (CONVENTIONS.md "Naming").</summary>
internal static class GiftListProjectionDocumentMapper
{
    /// <summary>
    /// Removed items (tombstones — see <see cref="GiftItemProjectionDocument.IsRemoved"/>'s own
    /// doc comment) are filtered out here, never on the way in: this is the one place that
    /// decides what a query response looks like, so it is the one place a future second query
    /// cannot forget the filter.
    /// </summary>
    public static GiftListProjection ToProjection(GiftListProjectionDocument document) => new(
        document.Id,
        new DateTimeOffset(document.ExpiresAt, TimeSpan.Zero),
        document.IsDeleted,
        document.Items
            .Where(item => !item.IsRemoved)
            .Select(item => item.ItemId)
            .ToList());
}
