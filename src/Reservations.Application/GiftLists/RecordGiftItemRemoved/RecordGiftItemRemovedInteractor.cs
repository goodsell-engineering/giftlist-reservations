using BuildingBlocks.Results;

namespace Reservations.Application.GiftLists.RecordGiftItemRemoved;

/// <summary>Pure pass-through — see <c>RecordGiftListCreatedInteractor</c>'s own doc comment.</summary>
internal sealed class RecordGiftItemRemovedInteractor(IGiftListProjectionRepository giftLists)
    : IRecordGiftItemRemoved
{
    public async Task<Result<RecordGiftItemRemovedResponse>> Handle(
        RecordGiftItemRemovedRequest request, CancellationToken cancellationToken)
    {
        await giftLists.ApplyItemRemovedAsync(request, cancellationToken);
        return new RecordGiftItemRemovedResponse();
    }
}
