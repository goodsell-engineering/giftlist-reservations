namespace Reservations.Domain.Reservations;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") for a <see cref="Reservation"/> itself —
/// the Mongo document's own <c>_id</c>, distinct from <see cref="GiftListId"/>/
/// <see cref="GiftItemId"/>, the pair a reservation is <em>about</em>. Minted server-side by
/// <see cref="New"/>, the same way <c>Identity.Domain.Users.UserId</c> is: unlike a gift list or
/// gift item id, nothing outside this service ever needs to know a reservation's id before it
/// exists.
/// </summary>
public readonly record struct ReservationId(Guid Value)
{
    public static ReservationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
