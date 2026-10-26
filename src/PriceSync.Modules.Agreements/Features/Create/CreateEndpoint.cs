using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.Create;

internal static class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(CreateRequest request, HttpContext httpContext,
        CreateHandler handler, CancellationToken cancellationToken)
    {
        var keys = httpContext.Request.Headers["Idempotency-Key"];
        var command = new CreateCommand(request.TradingRelationshipId, keys.Count == 1 ? keys[0] : null);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result switch
        {
            CreateResult.Created created => CreatedResponse(created, httpContext),
            CreateResult.ActingOrganizationRequired => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required }),
            CreateResult.Forbidden => Results.Problem(statusCode: 403,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }),
            CreateResult.InvalidRelationshipId => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["tradingRelationshipId"] = ["Trading Relationship is required."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" }),
            CreateResult.NotFound => Results.Problem(statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" }),
            CreateResult.RelationshipEnded => Results.Problem(statusCode: 409,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_relationship_ended" }),
            CreateResult.InvalidIdempotencyKey => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_idempotency_key" }),
            CreateResult.KeyConflict => Results.Problem(statusCode: 409,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_creation_key_conflict" }),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }

    private static IResult CreatedResponse(CreateResult.Created result, HttpContext httpContext)
    {
        httpContext.Response.Headers.Location = result.Location;
        return Results.Content(result.ResponseBody, "application/json", statusCode: result.StatusCode);
    }
}
