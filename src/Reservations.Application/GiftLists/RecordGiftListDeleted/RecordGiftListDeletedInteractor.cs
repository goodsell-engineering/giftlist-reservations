using BuildingBlocks.Results;

namespace Reservations.Application.GiftLists.RecordGiftListDeleted;

/// <summary>Pure pass-through — see <c>RecordGiftListCreatedInteractor</c>'s own doc comment.</summary>
internal sealed class RecordGiftListDeletedInteractor(IGiftListProjectionRepository giftLists)
    : IRecordGiftListDeleted
{
    public async Task<Result<RecordGiftListDeletedResponse>> Handle(
        RecordGiftListDeletedRequest request, CancellationToken cancellationToken)
    {
        await giftLists.ApplyListDeletedAsync(request, cancellationToken);
        return new RecordGiftListDeletedResponse();
    }
}
