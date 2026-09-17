namespace Reservations.Domain.Common;

/// <summary>
/// Marker for a fact an aggregate raised (ARCHITECTURE.md "Domain events are not integration
/// events" — never leaves the process; an Infrastructure mapper translates it into an integration
/// event). Replaces a bare <c>object</c> on <c>Reservation.DomainEvents</c> so a mapper's
/// exhaustive switch has a closed, typed set to pattern-match against instead of "anything".
/// </summary>
/// <remarks>
/// Declared per-service, in this BCL-only Domain project, rather than in the shared
/// BuildingBlocks package: <c>Domain_ShouldNotReferenceBuildingBlocks</c> [AT] (CONVENTIONS.md
/// "Project reference graph") forbids Domain from referencing BuildingBlocks at all, for any
/// reason — the rule is mechanical, not scoped to <c>Result&lt;T&gt;</c> specifically. A shared
/// marker would need Domain to take that reference, so each service's Domain declares its own
/// instead, exactly as <c>Identity.Domain.Common.IDomainEvent</c> and
/// <c>GiftLists.Domain.Common.IDomainEvent</c> do. The duplication is the accepted cost of the
/// ring boundary, same as the architecture test files themselves being copied verbatim per repo
/// (GL-10).
/// </remarks>
public interface IDomainEvent;
