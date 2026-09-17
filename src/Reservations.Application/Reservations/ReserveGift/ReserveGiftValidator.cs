using BuildingBlocks.Results;
using Reservations.Application.Common;
using Reservations.Application.GiftLists;

namespace Reservations.Application.Reservations.ReserveGift;

/// <summary>
/// Structural validation only — "is this a well-formed request", not a business rule.
/// Reservability (does the list exist, is it deleted or expired, does the item exist on it) is
/// <see cref="ReserveGiftInteractor"/>'s job, against the gift-list projection, not this
/// validator's (CONVENTIONS.md "Use cases" — validation is the composition root's decorator's
/// job, but only for what can be judged from the request alone).
/// </summary>
internal sealed class ReserveGiftValidator : IValidator<ReserveGiftRequest>
{
    public Result Validate(ReserveGiftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        return Result.Success();
    }
}
