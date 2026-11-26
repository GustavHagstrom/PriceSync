using Microsoft.AspNetCore.Http;
using PriceSync.Modules.Agreements.Acceptance;

namespace PriceSync.Modules.Agreements.Features.AcceptVersion;

internal static class AcceptVersionEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, Guid versionId, AcceptVersionHandler handler,
        CancellationToken cancellationToken) =>
        AcceptanceResponse.ToHttpResult(await handler.HandleAsync(new AcceptVersionCommand(agreementId, versionId), cancellationToken));
}
