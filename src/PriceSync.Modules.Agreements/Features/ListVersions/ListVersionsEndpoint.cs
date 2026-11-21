using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal static class ListVersionsEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, [FromQuery] string? pageSize, [FromQuery] string? cursor,
        ListVersionsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListVersionsQuery(agreementId, pageSize, cursor), cancellationToken);
        return result switch
        {
            ListVersionsResult.Success success => Results.Ok(success.Response),
            ListVersionsResult.ActingOrganizationRequired => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required }),
            ListVersionsResult.Forbidden => Results.Problem(statusCode: 403,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }),
            ListVersionsResult.NotFound => Results.Problem(statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" }),
            ListVersionsResult.ValidationFailed invalid => Results.ValidationProblem(invalid.Errors,
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" }),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }
}
