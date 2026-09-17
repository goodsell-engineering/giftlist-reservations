using Reservations.Domain.Common;

namespace Reservations.Domain.Reservations.Events;

/// <summary>
/// Raised when a gift item is successfully reserved. Never leaves the process — an Infrastructure
/// mapper translates this into the wire-facing <c>GiftReservedV1</c> integration event for
/// publication (ARCHITECTURE.md "Domain events are not integration events").
///
/// Carries only <see cref="ListId"/>, <see cref="ItemId"/> and <see cref="ReservedAt"/> — deliberately
/// not <see cref="Reservation.Id"/> and deliberately not <see cref="Reservation.ReleaseSecret"/>.
/// ARCHITECTURE.md "Reservation privacy" is a hard guarantee enforced by the absence of
/// data: no field on this event may ever let two reservations be correlated to one browser, and a
/// release secret is exactly that kind of field for whoever holds it. The interactor that raises
/// this event already has the aggregate in hand for its own reply, so nothing here needs to carry
/// the secret merely to get it there.
/// </summary>
public sealed record GiftReserved(GiftListId ListId, GiftItemId ItemId, DateTimeOffset ReservedAt) : IDomainEvent;
