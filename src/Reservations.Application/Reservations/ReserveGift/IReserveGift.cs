using Reservations.Application.Common;

namespace Reservations.Application.Reservations.ReserveGift;

/// <summary>
/// The named input port for "reserve a gift item" (CONVENTIONS.md "Naming"). Declared for the
/// naming convention and for readability on <see cref="ReserveGiftInteractor"/>'s own base list —
/// see <see cref="IInteractor{TRequest,TResponse}"/>'s doc comment for why nothing resolves this
/// specific type from the container.
/// </summary>
public interface IReserveGift : IInteractor<ReserveGiftRequest, ReserveGiftResponse>;
