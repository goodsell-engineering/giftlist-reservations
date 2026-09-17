namespace Reservations.Contracts.Reservations;

/// <summary>
/// Command: ask Reservations to reserve a gift item. Sent with <c>bus.Send()</c> and answered
/// with <see cref="ReserveGiftReply"/> via <c>bus.Reply()</c> — one of the three request/reply
/// flows (ARCHITECTURE.md "Command → event flow", alongside Login/SignUp), since the caller needs
/// a yes/no (did someone beat me to it?) before it can respond to whoever is waiting.
/// </summary>
public sealed record ReserveGift(Guid ListId, Guid ItemId);
