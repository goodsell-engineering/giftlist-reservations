namespace Reservations.Domain.Reservations;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") for the gift list a
/// <see cref="Reservation"/> belongs to. Wraps the same <see cref="Guid"/> GiftLists knows as
/// <c>GiftLists.Domain.GiftLists.GiftListId</c>, but is its own type rather than a reference to
/// GiftLists' — Reservations' <c>Domain</c> references nothing outside the BCL (CONVENTIONS.md
/// "Project reference graph"), and services only ever know each other by opaque id, never by
/// importing one another's aggregate types. No <c>New()</c>: unlike <see cref="ReservationId"/>,
/// this id is never minted here — it always arrives from a caller that already has it (the list
/// the reservation is against, created by GiftLists).
/// </summary>
public readonly record struct GiftListId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
