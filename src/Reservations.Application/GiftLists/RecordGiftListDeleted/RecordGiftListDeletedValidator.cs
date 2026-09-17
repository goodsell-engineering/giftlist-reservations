using BuildingBlocks.Results;
using Reservations.Application.Common;

namespace Reservations.Application.GiftLists.RecordGiftListDeleted;

/// <summary>Structural validation only — see <c>RecordGiftListCreatedValidator</c>'s own doc comment.</summary>
internal sealed class RecordGiftListDeletedValidator : IValidator<RecordGiftListDeletedRequest>
{
    public Result Validate(RecordGiftListDeletedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        return Result.Success();
    }
}
