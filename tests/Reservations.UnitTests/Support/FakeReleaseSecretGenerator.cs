using Reservations.Application.Common;
using Reservations.Domain.Reservations;

namespace Reservations.UnitTests.Support;

/// <summary>Hand-written fake — always returns a fixed, already-valid <see cref="ReleaseSecret"/>-shaped string, so interactor tests never depend on real randomness.</summary>
internal sealed class FakeReleaseSecretGenerator(string? value = null) : IReleaseSecretGenerator
{
    private readonly string _value = value ?? new string('a', ReleaseSecret.Length);

    public string Generate() => _value;
}
