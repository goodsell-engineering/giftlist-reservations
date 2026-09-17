using BuildingBlocks.Results;
using Reservations.Application.Common;

namespace Reservations.Application.GiftLists.RecordGiftItemAdded;

/// <summary>Structural validation only — see <c>RecordGiftListCreatedValidator</c>'s own doc comment.</summary>
internal sealed class RecordGiftItemAddedValidator : IValidator<RecordGiftItemAddedRequest>
{
    public Result Validate(RecordGiftItemAddedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        return Result.Success();
    }
}
