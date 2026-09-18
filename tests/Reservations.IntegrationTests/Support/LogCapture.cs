using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Reservations.IntegrationTests.Support;

/// <summary>
/// Captures log entries written by the Reservations host, so a test can assert that something was
/// (or, for GL-37, was NOT) logged rather than only that a call did not throw. Ported from
/// <c>Identity.IntegrationTests.Support.LogCapture</c>/<c>GiftLists.IntegrationTests.Support.LogCapture</c>
/// — same shape, same reasoning: a message-only assertion here is the cheapest honest way to prove
/// the <c>Logging&lt;,&gt;</c> decorator (CONVENTIONS.md "Use cases") never puts a
/// <c>ReleaseSecret</c>'s plaintext into a log line, which nothing about the type system enforces
/// on its own — <c>ReserveGiftResponse.ReleaseSecret</c> is a plain <see cref="string"/>, not the
/// redacting <c>Reservations.Domain.Reservations.ReleaseSecret</c> value object (it has to be, to
/// reach the wire reply), so a future <c>logger.LogInformation(response.ReleaseSecret)</c> would
/// compile and leak it for real.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
