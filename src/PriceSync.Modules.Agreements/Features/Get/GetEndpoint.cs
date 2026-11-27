using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.Get;

internal static class GetEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, GetHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetQuery(agreementId), cancellationToken);
        return result switch
        {
            GetResult.Success success => Results.Ok(success.Response),
            GetResult.ActingOrganizationRequired => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required }),
            GetResult.Forbidden => Results.Problem(statusCode: 403,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }),
            GetResult.NotFound => Results.Problem(statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" }),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }
}
