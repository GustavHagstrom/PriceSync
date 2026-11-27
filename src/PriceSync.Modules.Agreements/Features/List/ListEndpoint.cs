using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.List;

internal static class ListEndpoint
{
    internal static async Task<IResult> HandleAsync([FromQuery] string? tradingRelationshipId,
        [FromQuery] string? pageSize, [FromQuery] string? cursor, ListHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListQuery(tradingRelationshipId, pageSize, cursor), cancellationToken);
        return result switch
        {
            ListResult.Success success => Results.Ok(success.Response),
            ListResult.ActingOrganizationRequired => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required }),
            ListResult.Forbidden => Results.Problem(statusCode: 403,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }),
            ListResult.NotFound => Results.Problem(statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" }),
            ListResult.ValidationFailed invalid => Results.ValidationProblem(invalid.Errors,
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" }),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }
}
