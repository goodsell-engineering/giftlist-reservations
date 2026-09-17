namespace Reservations.Contracts.Reservations;

/// <summary>
/// The success reply to <see cref="ReserveGift"/>: the opaque <c>releaseSecret</c> that lets the
/// reserving browser undo its own reservation later (ARCHITECTURE.md "Data model"; ARCHITECTURE.md
/// "Reservation privacy" — returned here and ONLY here; never queried, projected or
/// published). Failure no longer travels through this type — a handler that fails replies with
/// <c>BuildingBlocks.Messaging.RequestReply.ReplyFault</c> instead, and the request/reply bridge
/// surfaces it as <c>Result&lt;ReserveGiftReply&gt;.Failure</c>, mirroring
/// <c>Identity.Contracts.Users.SignUpReply</c>'s own doc comment exactly.
/// </summary>
public sealed record ReserveGiftReply(string ReleaseSecret, DateTimeOffset ReservedAt);
