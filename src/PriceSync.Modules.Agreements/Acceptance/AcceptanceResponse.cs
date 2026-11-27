using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Acceptance;

internal sealed record AcceptanceResponse(bool Accepted, Guid? AcceptedByPersonId, DateTimeOffset? AcceptedAt)
{
    internal static IResult ToHttpResult(AcceptanceResult result) => result switch
    {
        AcceptanceResult.Success success => Results.Ok(new AcceptanceResponse(success.Acceptance is not null,
            success.Acceptance?.AcceptedByPersonId.Value, success.Acceptance?.AcceptedAt)),
        AcceptanceResult.ActingOrganizationRequired => Problem(400, OrganizationContextErrors.Required),
        AcceptanceResult.Forbidden => Problem(403, "forbidden"),
        AcceptanceResult.NotFound => Problem(404, "agreement_version_not_found"),
        AcceptanceResult.RelationshipEnded => Problem(409, "agreement_relationship_ended"),
        _ => throw new InvalidOperationException("Unexpected result type."),
    };

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
