using System.Text.RegularExpressions;

namespace Reservations.Domain.Reservations;

/// <summary>
/// The opaque, unguessable capability that lets the browser which reserved a gift undo its own
/// reservation later, without logging in and without this service ever learning who that browser
/// belongs to (ARCHITECTURE.md "Data model" — <c>reservation.reservations</c> carries
/// <c>releaseSecret</c>; ARCHITECTURE.md "Reservation privacy" — "opaque, random, never returned
/// in a query, and not correlatable to a person"; CONVENTIONS.md "Domain modelling" — value
/// object for its length/alphabet rule).
/// </summary>
/// <remarks>
/// Never constructed from a caller-chosen value — like
/// <c>GiftLists.Domain.GiftLists.ShareToken</c>/<c>Identity.Domain.Users.PasswordHash</c>, the raw
/// string always comes from an Infrastructure port
/// (<c>Reservations.Application.Common.IReleaseSecretGenerator</c>) using a cryptographically
/// secure random source; this type only ever validates and wraps the result. Domain has no
/// project references (CONVENTIONS.md "Project reference graph") and therefore cannot itself
/// decide how "random" is produced.
///
/// Longer than <c>ShareToken</c> (21 chars) on purpose: a share token only ever grants read
/// access to one list's already-public-to-guests state, while a release secret grants a
/// destructive action (undoing someone else's "first reserver wins" claim) with nothing else —
/// no session, no login — standing behind it, so the search space it must resist guessing is the
/// only defence there is. <see cref="ToString"/> redacts the value, mirroring
/// <c>Identity.Domain.Users.PasswordHash</c>'s own doc comment, so a stray log statement or
/// exception message never leaks a live capability.
/// </remarks>
public sealed class ReleaseSecret : IEquatable<ReleaseSecret>
{
    public const int Length = 32;

    private static readonly Regex Pattern = new(
        $@"^[0-9A-Za-z]{{{Length}}}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ReleaseSecret(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException($"Value must be a {Length}-character base62 string.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public bool Equals(ReleaseSecret? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as ReleaseSecret);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => "[redacted]";
}
