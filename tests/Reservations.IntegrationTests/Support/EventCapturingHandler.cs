using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace Reservations.IntegrationTests.Support;

/// <summary>Captures a real, deserialized <typeparamref name="TEvent"/> plus the wire's own <c>rbs2-msg-type</c> header off a real subscription.</summary>
internal sealed class EventCapturingHandler<TEvent>(EventCapture<TEvent> capture) : IHandleMessages<TEvent>
{
    public Task Handle(TEvent message)
    {
        MessageContext.Current.Headers.TryGetValue(Headers.Type, out var typeHeader);
        capture.Completion.TrySetResult((message, typeHeader));
        return Task.CompletedTask;
    }
}
