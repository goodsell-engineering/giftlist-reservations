namespace Reservations.Application.Common;

/// <summary>
/// Port over the cryptographically-random source that backs
/// <c>Reservations.Domain.Reservations.ReleaseSecret</c> (CONVENTIONS.md "Folder structure" —
/// genuinely domain-agnostic port, so it lives in <c>Common/</c> next to <see cref="IClock"/>
/// rather than under <c>Reservations/</c>; also keeps it out of
/// <c>NamingConventionTests.InputPorts_ShouldHaveAMatchingInteractorAndRequest</c>'s text scan,
/// the same reason <c>GiftLists.Application.Common.IShareTokenGenerator</c> lives in
/// <c>Common/</c>). Application never sees a random-number-generation library by name;
/// <see cref="Generate"/> returns a raw string that
/// <c>Reservations.Domain.Reservations.ReleaseSecret</c>'s constructor validates and wraps.
/// </summary>
public interface IReleaseSecretGenerator
{
    string Generate();
}
