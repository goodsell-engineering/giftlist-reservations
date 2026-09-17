namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>
/// Normalises an instant arriving on the wire to the precision this projection can actually
/// store, so ordering decisions compare like with like.
/// </summary>
/// <remarks>
/// <para>
/// The projection documents store instants as BSON <see cref="DateTime"/>, which is MILLISECOND
/// precision; a <see cref="DateTimeOffset"/> off the wire carries 100ns ticks. Without this, every
/// last-write-wins guard compared a value read back from Mongo (truncated) against an incoming one
/// (not truncated) — two different resolutions, which can invert a genuine ordering, not merely
/// mis-break a tie (see <see cref="GiftItemProjectionDocument.UpdatedAt"/>'s own reasoning, mirrored
/// from <c>Gateway.Infrastructure.GiftLists.Persistence.ProjectionInstants</c>).
/// </para>
/// <para>
/// Deliberately NOT shared with <c>Reservations.Domain.Common.Timestamps</c>: that one is a
/// statement about the <c>Reservation</c> aggregate's own resolution, this one is a statement
/// about what THIS projection's storage can represent, and the two happening to be a millisecond
/// is a coincidence rather than a coupling — exactly the reasoning Gateway's own copy states for
/// not sharing with GiftLists' <c>Timestamps</c>.
/// </para>
/// </remarks>
internal static class ProjectionInstants
{
    /// <summary>Converts to UTC and drops sub-millisecond ticks. Truncates, never rounds, so a value can only move toward the past.</summary>
    public static DateTime ToStoredPrecision(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMillisecond));
    }
}
