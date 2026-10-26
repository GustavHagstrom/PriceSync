using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed class PublishHandler(AgreementsDbContext db, TimeProvider clock, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
    IPriceListScopeVerifier lists)
{
    internal async Task<IResult> HandleAsync(Guid agreementId, PublishRequest request, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (agreementId == Guid.Empty) return Problem(404, "agreement_not_found");
        var id = new AgreementId(agreementId);
        var scope = await db.PriceAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (scope is null) return Problem(404, "agreement_not_found");
        var relationship = await relationships.GetAsync(scope.TradingRelationshipId, cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting) return Problem(404, "agreement_not_found");
        if (relationship.Status == TradingRelationshipStatus.Ended) return Problem(409, "agreement_relationship_ended");
        if (request.ValidFrom is null) return Invalid("validFrom", "ValidFrom is required.");
        if (request.ValidTo < request.ValidFrom) return Invalid("validTo", "ValidTo must be on or after ValidFrom.");
        if (request.ExpectedLatestVersionId.ValueKind != JsonValueKind.Null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["expectedLatestVersionId"] = ["First publication requires an explicit null predecessor."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });

        if (request.Coverage is not { Length: 1 }) return Invalid("coverage", "This stage requires exactly one General Price List.");
        if (request.GroupDiscounts.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null) &&
            (request.GroupDiscounts.ValueKind != JsonValueKind.Array || request.GroupDiscounts.GetArrayLength() != 0))
            return Invalid("groupDiscounts", "Group discounts are not supported yet.");
        var item = request.Coverage[0];
        if (item is null) return Invalid("coverage[0]", "Coverage is required.");
        if (item.Type != "general") return Invalid("coverage[0].type", "Only General coverage is supported yet.");
        if (item.PriceListId == Guid.Empty) return Invalid("coverage[0].priceListId", "Price List is required.");
        if (item.GeneralDiscount is not { } discount || discount is < 0 or > 100 || decimal.Round(discount, 1) != discount)
            return Invalid("coverage[0].generalDiscount", "An explicit discount from 0 to 100 with at most one decimal place is required.");
        var verification = await lists.VerifyAsync(acting, relationship.CustomerOrganizationId,
            [new(new PriceListId(item.PriceListId), PriceListType.General)], cancellationToken);
        if (verification is PriceListVerificationResult.Invalid)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["coverage[0].priceListId"] = ["Invalid Price List scope."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
        if (verification is PriceListVerificationResult.Unavailable)
            return Results.Problem(statusCode: 503, extensions: new Dictionary<string, object?>
                { ["code"] = "agreement_list_verification_unavailable", ["retryable"] = true });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var agreement = await db.PriceAgreements.FromSqlInterpolated($"""
            SELECT * FROM agreements.price_agreements WHERE "Id" = {agreementId} FOR UPDATE
            """).SingleAsync(cancellationToken);
        if (agreement.LatestVersionId is not null) return Problem(409, "agreement_publication_conflict");
        var version = PriceAgreementVersion.First(agreement, request.ValidFrom!.Value, request.ValidTo, person.PersonId, clock.GetUtcNow());
        var coverage = AgreementVersionCoverage.General(version.Id, new(item.PriceListId), item.GeneralDiscount!.Value);
        db.Versions.Add(version);
        db.Coverage.Add(coverage);
        // Insert the version before moving the constrained latest pointer, within the same local transaction.
        await db.SaveChangesAsync(cancellationToken);
        agreement.PublishFirst(version);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/agreements/{agreement.Id}/versions/{version.Id}", VersionResponse.From(version, coverage));
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static IResult Invalid(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] },
        extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
}
