using Reservations.Domain.Reservations;

namespace Reservations.IntegrationTests.Support;

/// <summary>
/// Release secrets for tests, in the one shape this service can actually emit: exactly
/// <see cref="ReleaseSecret.Length"/> base62 characters — mirrors
/// <c>Gateway.IntegrationTests.Support.ShareTokens</c>'s own doc comment and reasoning exactly,
/// substituting this service's own value object. Unlike that helper, this repo's IntegrationTests
/// project already references <c>Reservations.Domain</c> (via <c>Reservations.Host</c>), so this
/// draws the length from <see cref="ReleaseSecret.Length"/> itself rather than a second, hand-kept copy of it.
/// </summary>
internal static class ReleaseSecrets
{
    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// A fresh, valid secret drawn from the whole base62 alphabet. <see cref="Random.Shared"/>,
    /// not a cryptographic source: this is a fixture, never a real capability, so unguessability
    /// is not a property under test here — <c>ReleaseSecretGenerator</c> owns that, on the
    /// production side.
    /// </summary>
    public static string New() => new(Random.Shared.GetItems<char>(Base62, ReleaseSecret.Length));
}
