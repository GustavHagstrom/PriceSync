using Microsoft.AspNetCore.Http;
using PriceSync.Modules.Agreements.Acceptance;

namespace PriceSync.Modules.Agreements.Features.GetVersionAcceptance;

internal static class GetVersionAcceptanceEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, Guid versionId, GetVersionAcceptanceHandler handler,
        CancellationToken cancellationToken) =>
        AcceptanceResponse.ToHttpResult(await handler.HandleAsync(new GetVersionAcceptanceQuery(agreementId, versionId), cancellationToken));
}
