namespace PriceSync.Modules.Agreements.Features.Create;

internal abstract record CreateResult
{
    // Both creation and matching retries return the original successful response.
    internal sealed record Created(int StatusCode, string Location, string ResponseBody) : CreateResult;
    internal sealed record KeyConflict : CreateResult;
    internal sealed record ActingOrganizationRequired : CreateResult;
    internal sealed record Forbidden : CreateResult;
    internal sealed record InvalidRelationshipId : CreateResult;
    internal sealed record NotFound : CreateResult;
    internal sealed record RelationshipEnded : CreateResult;
    internal sealed record InvalidIdempotencyKey : CreateResult;
}
