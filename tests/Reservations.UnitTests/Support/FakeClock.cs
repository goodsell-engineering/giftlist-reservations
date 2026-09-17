using Reservations.Application.Common;

namespace Reservations.UnitTests.Support;

/// <summary>Hand-written fake (CONVENTIONS.md "Testing" — preferred over a mock for a port used across many tests): settable, deterministic "now" for interactor/value-object tests.</summary>
internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
