using Reservations.Application.Common;
using Reservations.Application.GiftLists.RecordGiftItemAdded;
using GiftLists.Contracts.GiftLists.Events;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Reservations.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="GiftListCreatedV1Handler"/> for the rationale.</summary>
internal sealed class GiftItemAddedV1Handler(
    IInteractor<RecordGiftItemAddedRequest, RecordGiftItemAddedResponse> recordGiftItemAdded)
    : IHandleMessages<GiftItemAddedV1>
{
    public async Task Handle(GiftItemAddedV1 message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new RecordGiftItemAddedRequest(message.ListId, message.ItemId, message.AddedAt);
        await recordGiftItemAdded.Handle(request, cancellationToken);
    }
}
