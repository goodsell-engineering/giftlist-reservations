namespace Reservations.IntegrationTests.Support;

/// <summary>Registered as a singleton in a subscriber's own DI container so a test can await what its handler received. Mirrors <c>Identity.IntegrationTests.Support.EventCapture&lt;TEvent&gt;</c>.</summary>
internal sealed class EventCapture<TEvent>
{
    public TaskCompletionSource<(TEvent Body, string? TypeHeader)> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
