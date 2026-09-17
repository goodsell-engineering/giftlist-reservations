namespace Reservations.Domain.Reservations;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") for the gift item a
/// <see cref="Reservation"/> is against — keeps <c>Reserve(listId, itemId)</c>-shaped calls from
/// silently compiling with the two ids swapped. Wraps the same <see cref="Guid"/> GiftLists knows
/// as <c>GiftLists.Domain.GiftLists.GiftItemId</c>; see <see cref="GiftListId"/>'s own doc
/// comment for why this is a separate, per-service copy rather than a shared or imported type.
/// No <c>New()</c> — always supplied by the caller, never minted here.
/// </summary>
public readonly record struct GiftItemId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
