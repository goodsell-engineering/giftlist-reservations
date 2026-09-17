using BuildingBlocks.Results;

namespace Reservations.Application.Common;

/// <summary>
/// The shape every use case implements: one request in, one <see cref="Result{T}"/> out
/// (CONVENTIONS.md "Use cases"). Mirrors <c>Gateway.Application.Common.IInteractor</c> and
/// <c>GiftLists.Application.Common.IInteractor</c> — each use case still declares its own
/// specifically-named, empty input port (<c>IRecordGiftListCreated</c> and friends) purely for
/// the naming-convention text scan, while the composition root and the
/// <see cref="Validating{TRequest,TResponse}"/>/<c>Logging&lt;,&gt;</c> decorators all key on this
/// generic interface instead — that is what lets one decorator pair cover every interactor in
/// this service. Nothing ever resolves a named port from the container (CONVENTIONS.md "Use
/// cases"; GL-84's <c>InputPortInjectionRuleTests</c>).
/// </summary>
public interface IInteractor<in TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken);
}
