using Reservations.Application.GiftLists.RecordGiftItemAdded;
using Reservations.Application.GiftLists.RecordGiftItemRemoved;
using Reservations.Application.GiftLists.RecordGiftListCreated;
using Reservations.Application.GiftLists.RecordGiftListDeleted;
using Reservations.Domain.Reservations;

namespace Reservations.Application.GiftLists;

/// <summary>
/// Port lives beside the domain it serves (CONVENTIONS.md "Folder structure"), not in a shared
/// Abstractions bucket. One repository over the one projection this service has
/// (ARCHITECTURE.md "Consuming other services' events: anti-corruption layer" — "Reservation's
/// local store of 'what is reservable', built from those events, is a projection, not an
/// aggregate: it holds no invariants, is rebuildable from the event stream, and is never written
/// by a use case except through the event handler.").
///
/// The four <c>Apply*</c> methods are this projection's whole redelivery/reordering story
/// (CONVENTIONS.md "Messaging") — each is expected to be an idempotent, last-write-wins upsert,
/// never a blind insert or append, mirroring
/// <c>Gateway.Application.GiftLists.IGiftListProjectionRepository</c>'s own five (this port omits
/// only <c>ApplyListRenamedAsync</c> — see <c>GiftListListRenamedV1Handler</c>'s absence in
/// Infrastructure for why: nothing here ever reads a list's name, so there is no field for a
/// rename to update).
/// </summary>
public interface IGiftListProjectionRepository
{
    /// <summary>
    /// <see langword="null"/> both when no <c>GiftListCreatedV1</c> has been seen for this list yet
    /// and when the projection genuinely has no row — the caller cannot and need not tell those
    /// apart (CONVENTIONS.md "Messaging": redelivery/reordering is normal, not an error).
    ///
    /// Unlike <c>Gateway.Application.GiftLists.IGiftListProjectionRepository.FindByIdAsync</c>,
    /// which filters out a deleted list entirely (owner/guest queries have no use for a row that
    /// is gone), a deleted list here is still returned — with <see cref="GiftListProjection.IsDeleted"/>
    /// <see langword="true"/> — because GL-36's <c>ReserveGift</c> needs to tell "no such list
    /// exists" (<see langword="null"/>) apart from "this list existed but is no longer
    /// reservable" (a non-null projection with <c>IsDeleted == true</c>), the same way it will
    /// need to compare <see cref="GiftListProjection.ExpiresAt"/> against its own clock rather
    /// than have this port pre-decide "expired" on its behalf.
    /// </summary>
    Task<GiftListProjection?> FindByIdAsync(GiftListId listId, CancellationToken cancellationToken);

    /// <exception cref="GiftListProjectionApplyExhaustedException">
    /// The compare-and-set retry loop exhausted its attempt cap — a sustained, pathological burst
    /// of concurrent writers to this same list.
    /// </exception>
    Task ApplyListCreatedAsync(RecordGiftListCreatedRequest request, CancellationToken cancellationToken);

    /// <exception cref="GiftListProjectionApplyExhaustedException">See <see cref="ApplyListCreatedAsync"/>.</exception>
    Task ApplyListDeletedAsync(RecordGiftListDeletedRequest request, CancellationToken cancellationToken);

    /// <exception cref="GiftListProjectionApplyExhaustedException">See <see cref="ApplyListCreatedAsync"/>.</exception>
    Task ApplyItemAddedAsync(RecordGiftItemAddedRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Must never simply be a no-op when the item is not yet present — a redelivered/reordered
    /// remove arriving <em>before</em> its matching add (both for the same item) is a real hazard
    /// (mirrors <c>Gateway.Application.GiftLists.IGiftListProjectionRepository.ApplyItemRemovedAsync</c>'s
    /// own doc comment). The implementation is expected to leave a tombstone so a later, but
    /// chronologically older, add is recognised as stale and ignored.
    /// </summary>
    /// <exception cref="GiftListProjectionApplyExhaustedException">See <see cref="ApplyListCreatedAsync"/>.</exception>
    Task ApplyItemRemovedAsync(RecordGiftItemRemovedRequest request, CancellationToken cancellationToken);
}
