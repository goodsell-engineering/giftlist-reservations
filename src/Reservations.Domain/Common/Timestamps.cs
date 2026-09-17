namespace Reservations.Domain.Common;

/// <summary>
/// An instant in this domain has MILLISECOND resolution. This normalises one to that resolution.
/// </summary>
/// <remarks>
/// <para>
/// The BSON dates <c>ReservationDocument</c> stores are milliseconds, while a
/// <see cref="DateTimeOffset"/> built from <c>IClock.UtcNow</c> carries 100ns ticks — so an
/// aggregate built in memory can hold precision storage silently discards, and a reloaded
/// reservation would not equal the one saved. Normalising at construction, not only in the
/// persistence mapper, is what makes "saved equals reloaded" true by construction rather than
/// true only after a round trip.
/// </para>
/// <para>
/// <c>GiftLists.Domain.Common.Timestamps</c> and <c>Identity.Domain.Common.Timestamps</c> are the
/// same rule (GL-63/GL-66), each its own copy: this cannot move to BuildingBlocks because Domain
/// references nothing outside the BCL (CONVENTIONS.md "Project reference graph"), so every
/// service carries its own, exactly as <c>Reservation</c>'s typed ids do.
/// </para>
/// </remarks>
public static class Timestamps
{
    /// <summary>Drops sub-millisecond ticks, preserving the offset.</summary>
    public static DateTimeOffset ToStoredPrecision(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));
}
