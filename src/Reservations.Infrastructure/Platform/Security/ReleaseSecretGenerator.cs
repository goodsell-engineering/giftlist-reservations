using System.Security.Cryptography;
using Reservations.Application.Common;
using Reservations.Domain.Reservations;

namespace Reservations.Infrastructure.Platform.Security;

/// <summary>
/// The real <see cref="IReleaseSecretGenerator"/> — belongs to no domain (purely mechanical
/// randomness, like <c>GiftLists.Infrastructure.Platform.Security.ShareTokenGenerator</c>/
/// <c>Identity.Infrastructure.Platform.Security.BCryptPasswordHasher</c>), so it lives in
/// Platform/Security/ (CONVENTIONS.md "Folder structure").
/// </summary>
internal sealed class ReleaseSecretGenerator : IReleaseSecretGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate()
    {
        // A cryptographically secure source (ARCHITECTURE.md "Reservation privacy" —
        // "opaque, random ... not correlatable to a person"), not System.Random: this secret is a
        // bearer capability with nothing else — no session, no login — behind it, so
        // predictability would let a stranger release someone else's reservation. The small
        // modulo bias this leaves (256 % 62 != 0) is an acceptable trade for this demo's threat
        // model, mirroring ShareTokenGenerator's own reasoning exactly.
        var bytes = RandomNumberGenerator.GetBytes(ReleaseSecret.Length);
        var chars = new char[ReleaseSecret.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
