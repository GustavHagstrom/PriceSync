using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceSync.Modules.Agreements.Features.Create;
using PriceSync.Modules.Agreements.Features.Get;
using PriceSync.Modules.Agreements.Features.List;
using PriceSync.Modules.Agreements.Features.ListVersions;
using PriceSync.Modules.Agreements.Features.PublishVersion;

namespace PriceSync.Modules.Agreements;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAgreementsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Agreements").RequireAuthorization();
        const string selection = "/trading-relationships/{relationshipId:guid}/agreement-selection";
        group.MapGet(selection, Features.AgreementSelection.AgreementSelectionEndpoints.GetAsync)
            .WithName("GetAgreementSelection")
            .WithSummary("Get the current or historical agreement selection for a trading relationship")
            .WithDescription("Supply at most one inclusive boundary: `asOfUtc` (an ISO 8601 timestamp with an explicit offset) or `throughSequence` (a nonnegative sequence). Historical responses have an empty warnings list.")
            .Produces<Features.AgreementSelection.AgreementSelectionResponse>();
        group.MapGet(selection + "/history", Features.AgreementSelection.AgreementSelectionEndpoints.GetHistoryAsync)
            .WithName("GetAgreementSelectionHistory")
            .WithSummary("List agreement selection transitions for a trading relationship")
            .WithDescription("Returns transitions in ascending sequence order. Page size defaults to 50 and is capped at 100. Pass `nextAfterSequence` as `afterSequence` to continue.")
            .Produces<Features.AgreementSelection.AgreementSelectionHistoryResponse>();
        group.MapPut(selection, Features.AgreementSelection.AgreementSelectionEndpoints.PutAsync)
            .WithName("ChangeAgreementSelection")
            .WithSummary("Change the current agreement selection for a trading relationship")
            .WithDescription("Supply both version IDs. An explicit null means no active version. Reload the current selection after a conflict.")
            .Produces<Features.AgreementSelection.AgreementSelectionResponse>();
        const string acceptance = "/agreements/{agreementId:guid}/versions/{versionId:guid}/acceptance";
        group.MapGet(acceptance, Features.GetVersionAcceptance.GetVersionAcceptanceEndpoint.HandleAsync)

            .WithName("GetAgreementVersionAcceptance")
            .WithSummary("Get the customer's acceptance of an agreement version")
            .WithDescription("The supplier and customer can check whether the customer has accepted this version and see the acceptance details when available.");
        group.MapPost(acceptance, Features.AcceptVersion.AcceptVersionEndpoint.HandleAsync)

            .WithName("AcceptAgreementVersion")
            .WithSummary("Accept an agreement version")
            .WithDescription("Records acceptance of a specific published version on behalf of the customer organization.");
        group.MapPost("/agreements", CreateEndpoint.HandleAsync)
            .WithName("CreateAgreement")
            .WithSummary("Create an agreement")
            .WithDescription("The supplier creates an agreement for a trading relationship. Send an `Idempotency-Key` header and reuse it when retrying the same request.");
        group.MapPost("/agreements/{agreementId:guid}/versions", PublishVersionEndpoint.HandleAsync)
            .WithName("PublishAgreementVersion")
            .WithSummary("Publish an agreement version")
            .WithDescription("The supplier publishes a version with validity dates, price list coverage, and discounts. Supply the expected latest version ID, or `null` for the first publication.");
        group.MapPost("/agreements/{agreementId:guid}/versions/{versionId:guid}/withdraw",
            Features.WithdrawVersion.WithdrawVersionEndpoint.HandleAsync)
            .WithName("WithdrawAgreementVersion")
            .WithSummary("Withdraw an agreement version")
            .WithDescription("The supplier withdraws a specific published version and can provide a reason. The version remains available in the agreement history.");
        group.MapGet("/agreements/{agreementId:guid}", GetEndpoint.HandleAsync)
            .WithName("GetAgreement")
            .WithSummary("Get an agreement")
            .WithDescription("Returns agreement details, including its trading relationship and latest version ID, to the supplier or customer.");
        group.MapGet("/agreements/{agreementId:guid}/versions/{versionId:guid}",
            (Guid agreementId, Guid versionId, Features.GetVersion.GetVersionHandler handler, CancellationToken cancellationToken) =>
                handler.HandleAsync(agreementId, versionId, cancellationToken))
            .WithName("GetAgreementVersion")
            .WithSummary("Get an agreement version")
            .WithDescription("Returns the published terms and lifecycle status of a specific version, including its validity period, coverage, and discounts.");
        group.MapGet("/agreements", ListEndpoint.HandleAsync)
            .WithName("ListAgreements")
            .WithSummary("List agreements for a trading relationship")
            .WithDescription("Returns a page of agreements for the supplied `tradingRelationshipId`, newest first. Pass the returned `nextCursor` as `cursor` to continue.");
        group.MapGet("/agreements/{agreementId:guid}/recommendation",
            Features.Recommendation.RecommendationEndpoint.HandleAsync)
            .WithName("GetAgreementRecommendation")
            .WithSummary("Get the recommended agreement version")
            .WithDescription("Uses `evaluatedDate` to check validity and `knowledgeCutoff` to determine which publications and withdrawals were known. The recommendation is `null` when no version qualifies.");
        group.MapGet("/agreements/{agreementId:guid}/versions", ListVersionsEndpoint.HandleAsync)
            .WithName("ListAgreementVersions")
            .WithSummary("List versions of an agreement")
            .WithDescription("Returns published versions in reverse publication order, including withdrawn versions. Pass the returned `nextCursor` as `cursor` to continue.");
        return endpoints;
    }
}
