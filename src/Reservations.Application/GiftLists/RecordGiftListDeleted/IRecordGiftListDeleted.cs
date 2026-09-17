using Reservations.Application.Common;

namespace Reservations.Application.GiftLists.RecordGiftListDeleted;

/// <summary>The named input port for "record that GiftLists deleted a list" (CONVENTIONS.md "Naming") — see <c>IRecordGiftListCreated</c>'s own doc comment for why nothing resolves this from the container.</summary>
public interface IRecordGiftListDeleted : IInteractor<RecordGiftListDeletedRequest, RecordGiftListDeletedResponse>;
