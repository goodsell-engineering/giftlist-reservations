using BuildingBlocks.Results;

namespace Reservations.Application.GiftLists;

/// <summary>
/// Stable, machine-readable error codes for the Reservation-side gift-list projection use cases
/// (CONVENTIONS.md "Errors"), all <c>reservation.&lt;code&gt;</c> — two segments, the singular
/// service name (CONVENTIONS.md "Persistence"), applied uniformly. Mirrors
/// <c>Gateway.Application.GiftLists.GiftListErrors</c>'s own <c>InvalidId</c>.
/// </summary>
public static class GiftListErrors
{
    /// <summary>Shared across every request field that is a required id and arrived as <see cref="System.Guid.Empty"/> — the same semantic regardless of which field or which use case caught it.</summary>
    public static readonly Error InvalidId = new(
        "reservation.invalid_id", "A required identifier was missing or invalid.", ErrorKind.Validation);
}
