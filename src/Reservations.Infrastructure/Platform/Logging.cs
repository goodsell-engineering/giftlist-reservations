using BuildingBlocks.Results;
using Reservations.Application.Common;
using Microsoft.Extensions.Logging;

namespace Reservations.Infrastructure.Platform;

/// <summary>
/// The logging decorator (CONVENTIONS.md "Use cases"), generic over every use case — mirrors
/// <c>Gateway.Infrastructure.Platform.Logging&lt;,&gt;</c> and
/// <c>GiftLists.Infrastructure.Platform.Logging&lt;,&gt;</c> exactly. Lives here rather than
/// beside <c>Validating&lt;,&gt;</c> in Application/Common because it needs <see cref="ILogger"/>,
/// and Application references nothing beyond BuildingBlocks (CONVENTIONS.md "Project reference
/// graph"). Registered after <c>Validating&lt;,&gt;</c> in the composition root, so it is the
/// outermost decorator and also observes (and logs) a validation failure, not just a business one
/// — Validation, then Logging, the same order in every service.
/// </summary>
internal sealed class Logging<TRequest, TResponse>(
    IInteractor<TRequest, TResponse> inner,
    ILogger<Logging<TRequest, TResponse>> logger) : IInteractor<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var result = await inner.Handle(request, cancellationToken);

        if (result.IsFailure)
        {
            // Error.Message is guaranteed PII-free (CONVENTIONS.md "Errors"), so it is safe to log
            // alongside the stable Code — neither ever identifies who made the request.
            logger.LogWarning(
                "{RequestType} failed: {ErrorCode} ({ErrorKind})",
                typeof(TRequest).Name,
                result.Error.Code,
                result.Error.Kind);
        }
        else
        {
            logger.LogInformation("{RequestType} succeeded.", typeof(TRequest).Name);
        }

        return result;
    }
}
