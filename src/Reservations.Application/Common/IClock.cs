namespace Reservations.Application.Common;

/// <summary>Genuinely domain-agnostic port (CONVENTIONS.md "Folder structure") — faked in tests for determinism.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
