using Reservations.Application.Common;

namespace Reservations.Application.GiftLists.RecordGiftItemRemoved;

/// <summary>The named input port for "record that GiftLists removed an item" (CONVENTIONS.md "Naming") — see <c>IRecordGiftListCreated</c>'s own doc comment for why nothing resolves this from the container.</summary>
public interface IRecordGiftItemRemoved : IInteractor<RecordGiftItemRemovedRequest, RecordGiftItemRemovedResponse>;
