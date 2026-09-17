using BuildingBlocks.Results;

namespace Reservations.Application.GiftLists.RecordGiftItemAdded;

/// <summary>Pure pass-through — see <c>RecordGiftListCreatedInteractor</c>'s own doc comment.</summary>
internal sealed class RecordGiftItemAddedInteractor(IGiftListProjectionRepository giftLists)
    : IRecordGiftItemAdded
{
    public async Task<Result<RecordGiftItemAddedResponse>> Handle(
        RecordGiftItemAddedRequest request, CancellationToken cancellationToken)
    {
        await giftLists.ApplyItemAddedAsync(request, cancellationToken);
        return new RecordGiftItemAddedResponse();
    }
}
