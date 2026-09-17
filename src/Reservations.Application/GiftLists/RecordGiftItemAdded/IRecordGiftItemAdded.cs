using Reservations.Application.Common;

namespace Reservations.Application.GiftLists.RecordGiftItemAdded;

/// <summary>The named input port for "record that GiftLists added an item" (CONVENTIONS.md "Naming") — see <c>IRecordGiftListCreated</c>'s own doc comment for why nothing resolves this from the container.</summary>
public interface IRecordGiftItemAdded : IInteractor<RecordGiftItemAddedRequest, RecordGiftItemAddedResponse>;
