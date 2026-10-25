using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Create;

internal static class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(CreateRequest request, HttpContext httpContext, ICurrentPerson person,
        ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
        CreateHandler handler, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required });
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        if (!Guid.TryParse(request.TradingRelationshipId, out var relationshipId) || relationshipId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tradingRelationshipId"] = ["Trading Relationship is required."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
        var relationship = await relationships.GetAsync(TradingRelationshipId.FromGuid(relationshipId), cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting)
            return Results.Problem(statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" });
        if (relationship.Status == TradingRelationshipStatus.Ended)
            return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "agreement_relationship_ended" });
        var keys = httpContext.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || keys[0] is not { Length: >= 1 and <= 200 } key || key.Any(char.IsControl))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "invalid_idempotency_key" });
        var replay = await handler.HandleAsync(person.PersonId, acting, TradingRelationshipId.FromGuid(relationshipId), key, cancellationToken);
        if (replay is null)
            return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "agreement_creation_key_conflict" });
        httpContext.Response.Headers.Location = replay.Location;
        return Results.Content(replay.ResponseBody, "application/json", statusCode: replay.ResponseStatus);
    }
}
