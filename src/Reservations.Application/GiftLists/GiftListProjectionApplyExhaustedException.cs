namespace Reservations.Application.GiftLists;

/// <summary>
/// Thrown when <c>GiftListProjectionRepository.ApplyAsync</c>'s compare-and-set retry loop
/// (<see cref="IGiftListProjectionRepository"/>'s four <c>Apply*</c> methods) exhausts its attempt
/// cap — a sustained, pathological burst of concurrent writers to ONE list that keeps beating this
/// attempt's read every single time. Mirrors
/// <c>Gateway.Application.GiftLists.GiftListProjectionApplyExhaustedException</c> exactly — see
/// that type's own doc comment for the full reasoning (why this is a thrown exception rather than
/// a <c>Result</c>, and why it is left to propagate into Rebus's own redelivery rather than
/// caught and retried again here).
/// </summary>
public sealed class GiftListProjectionApplyExhaustedException(Guid listId, int attempts)
    : Exception(
        $"Gift list projection '{listId}' could not be written after {attempts} attempts — " +
        "another writer kept winning the compare-and-set race every time. Failing loudly rather " +
        "than spinning forever; Rebus's own redelivery will retry this event with a fresh attempt " +
        "budget.")
{
    public Guid ListId { get; } = listId;

    public int Attempts { get; } = attempts;
}
