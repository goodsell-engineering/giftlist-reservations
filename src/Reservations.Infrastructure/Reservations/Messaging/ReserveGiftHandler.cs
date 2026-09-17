using BuildingBlocks.Messaging.RequestReply;
using Reservations.Application.Common;
using Reservations.Application.Reservations.ReserveGift;
using Reservations.Contracts.Reservations;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Reservations.Infrastructure.Reservations.Messaging;

/// <summary>
/// Thin by design (CONVENTIONS.md "Messaging" — no business logic in a handler): translate the
/// wire command into <see cref="ReserveGiftRequest"/>, call the one input port, translate the
/// <c>Result</c> back into a wire reply — a success payload, or <see cref="ReplyFault"/> on
/// failure (the bridge, not the reply type, carries failure; see
/// <see cref="ReserveGiftReply"/>'s doc comment). Everything that decides the outcome lives
/// behind <see cref="IInteractor{TRequest,TResponse}"/>. Mirrors
/// <c>Identity.Infrastructure.Users.Messaging.SignUpHandler</c>/<c>LoginHandler</c> exactly — this
/// is the same request/reply bridge (ARCHITECTURE.md "Command → event flow"), reused, not a new
/// pattern for this service.
/// </summary>
internal sealed class ReserveGiftHandler(IInteractor<ReserveGiftRequest, ReserveGiftResponse> reserveGift, IBus bus)
    : IHandleMessages<ReserveGift>
{
    public async Task Handle(ReserveGift message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();

        var request = new ReserveGiftRequest(message.ListId, message.ItemId);
        var result = await reserveGift.Handle(request, cancellationToken);

        object reply = result.Match<object>(
            onSuccess: response => new ReserveGiftReply(response.ReleaseSecret, response.ReservedAt),
            onFailure: ReplyFault.From);

        await bus.Reply(reply);
    }
}
