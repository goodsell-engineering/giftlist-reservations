namespace Reservations.Infrastructure.GiftLists.Persistence;

/// <summary>
/// The Mongo-facing shape of the Reservation service's own read model
/// (<c>reservation.giftListProjections</c>), kept separate from
/// <c>Reservations.Application.GiftLists.GiftListProjection</c> (ARCHITECTURE.md "Data that
/// crosses boundaries" — no <c>[Bson*]</c> attributes reach Application; none are needed here
/// either — <see cref="Id"/> auto-maps to <c>_id</c> and every field name camelCases via the
/// shared <c>BuildingBlocks.Persistence.MongoConventions</c> pack).
///
/// This document can exist in a partially-filled state the way
/// <c>Gateway.Infrastructure.GiftLists.Persistence.GiftListProjectionDocument</c> can, and for the
/// same reason: at-least-once, out-of-order delivery (CONVENTIONS.md "Messaging") means a
/// <c>GiftItemAddedV1</c>/<c>GiftItemRemovedV1</c>/<c>GiftListDeletedV1</c> can be processed for a
/// <see cref="Id"/> this service has not yet seen a <c>GiftListCreatedV1</c> for. Rather than drop
/// those events on the floor with nothing to replay them from,
/// <see cref="GiftListProjectionRepository"/> creates a stub row for them to land on —
/// <see cref="ExpiresAt"/> reads <see cref="DateTime.MinValue"/> until <c>GiftListCreatedV1</c>
/// itself arrives and fills it in unconditionally (nothing else ever writes it, so there is no
/// ordering hazard to guard there). A stub is invisible to
/// <see cref="GiftListProjectionRepository.FindByIdAsync"/>, which only ever matches on
/// <see cref="HasCreated"/> <see langword="true"/>.
/// </summary>
public sealed class GiftListProjectionDocument
{
    public required Guid Id { get; init; }

    /// <summary>
    /// <see langword="false"/> for a stub row created by an out-of-order item/delete event that
    /// arrived before this list's own <c>GiftListCreatedV1</c> — see this type's own doc comment.
    /// </summary>
    public required bool HasCreated { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public required bool IsDeleted { get; init; }

    /// <summary>Last-write-wins guard for <see cref="IsDeleted"/>. Null until a <c>GiftListDeletedV1</c> has been applied.</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Optimistic-concurrency counter, same idea as <c>ReservationDocument</c>'s own compound
    /// index is to that collection but for THIS one: <see cref="GiftListProjectionRepository"/>
    /// reads, mutates and version-filters its replace, retrying on a lost race rather than
    /// throwing — nobody is waiting synchronously on a projection write the way a command caller
    /// might be on an aggregate write, so a retry loop is the simpler choice here.
    /// </summary>
    public required long Version { get; init; }

    public required List<GiftItemProjectionDocument> Items { get; init; }
}
