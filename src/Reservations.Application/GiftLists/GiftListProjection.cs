using Reservations.Domain.Reservations;

namespace Reservations.Application.GiftLists;

/// <summary>
/// The Reservation service's own read model of a gift list (CONVENTIONS.md "Naming" — Noun +
/// "Projection"), built from GiftLists' integration events and consulted by
/// <c>ReserveGift</c> (GL-36) to decide what is reservable. Deliberately not the
/// <c>GiftLists.Domain.GiftLists.GiftList</c> aggregate — this is a different service's read of a
/// narrow slice of the same facts, kept in its own database (<c>reservation</c>) and updated
/// asynchronously, at least-once and out of order, by Rebus handlers (CONVENTIONS.md "Messaging";
/// ARCHITECTURE.md "Consuming other services' events: anti-corruption layer").
/// </summary>
/// <remarks>
/// Carries only what reservability actually depends on — <see cref="ListId"/>,
/// <see cref="ExpiresAt"/>, <see cref="IsDeleted"/> and which items currently exist. No name, no
/// owner, no share token: this service never displays a gift list, so there is nothing here for
/// those fields to serve, and ARCHITECTURE.md "Reservation privacy" already sets the tone of
/// collecting the minimum a service actually needs rather than everything an upstream event
/// happens to carry.
///
/// <see cref="ExpiresAt"/> is a plain fact, not a precomputed "has this expired" flag — GL-34's
/// whole point (ARCHITECTURE.md "Auth & sharing": "Reservation checks expiresAt on its own
/// projection rather than waiting for GiftListExpired to arrive") is that the caller compares this
/// against its own clock at the moment it matters, instead of trusting a delayed or (today,
/// unpublished) <c>GiftListExpired</c> event to have already landed.
///
/// <see cref="ListId"/> and <see cref="ItemIds"/> are the same strongly-typed
/// <see cref="GiftListId"/>/<see cref="GiftItemId"/> the <c>Reservation</c> aggregate itself uses
/// (CONVENTIONS.md "Domain modelling"), not a bare <see cref="Guid"/> — GL-36's <c>ReserveGift</c>
/// will hold a list id and an item id side by side when it calls this port, and typed ids are what
/// make that call impossible to compile with the two swapped. This is unlike
/// <c>Gateway.Application.GiftLists.GiftListProjection</c>, which has no <c>Domain</c> project to
/// borrow a typed id from at all (ARCHITECTURE.md "Mapping to Clean Architecture's rings" — the
/// Gateway has none); copying its bare-<see cref="Guid"/> shape here would have been the wrong
/// precedent to follow.
/// </remarks>
public sealed record GiftListProjection(
    GiftListId ListId,
    DateTimeOffset ExpiresAt,
    bool IsDeleted,
    IReadOnlyList<GiftItemId> ItemIds);
