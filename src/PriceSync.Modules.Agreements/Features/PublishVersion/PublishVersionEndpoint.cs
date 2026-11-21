using Microsoft.AspNetCore.Http;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal static class PublishVersionEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, PublishRequest request,
        PublishVersionHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new PublishVersionCommand(agreementId, request.ExpectedLatestVersionId,
            request.ValidFrom, request.ValidTo, request.Coverage, request.GroupDiscounts), cancellationToken);
        return result switch
        {
            PublishVersionResult.Created created => Results.Created(created.Location, created.Response),
            PublishVersionResult.Existing existing => Results.Ok(existing.Response),
            PublishVersionResult.ListVerificationUnavailable => Results.Problem(statusCode: 503,
                extensions: new Dictionary<string, object?>
                    { ["code"] = "agreement_list_verification_unavailable", ["retryable"] = true }),
            PublishVersionResult.ActingOrganizationRequired => Results.Problem(statusCode: 400,
                extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required }),
            PublishVersionResult.Forbidden => Results.Problem(statusCode: 403,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }),
            PublishVersionResult.NotFound => Results.Problem(statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" }),
            PublishVersionResult.RelationshipEnded => Results.Problem(statusCode: 409,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_relationship_ended" }),
            PublishVersionResult.PublicationConflict => Results.Problem(statusCode: 409,
                extensions: new Dictionary<string, object?> { ["code"] = "agreement_publication_conflict" }),
            PublishVersionResult.ValidationFailed invalid => Results.ValidationProblem(invalid.Errors,
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" }),
            _ => throw new InvalidOperationException("Unexpected result type."),
        };
    }
}
