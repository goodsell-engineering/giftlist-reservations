namespace Reservations.UnitTests.Support;

/// <summary>
/// A single ordered timeline shared by more than one fake, so a test can assert the order of
/// calls made ACROSS ports rather than only within one — mirrors
/// <c>GiftLists.UnitTests.Support.CallLog</c>'s own doc comment and reasoning exactly: separate
/// per-fake call lists cannot express "the save happened before the publish", only that each
/// happened.
/// </summary>
internal sealed class CallLog
{
    public List<string> Entries { get; } = [];

    public void Record(string name) => Entries.Add(name);
}
