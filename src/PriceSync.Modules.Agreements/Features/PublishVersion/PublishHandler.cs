using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed class PublishHandler(AgreementsDbContext db, TimeProvider clock, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
    IPriceListScopeVerifier lists, AgreementRoundingPolicy rounding)
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
        AgreementVersionId? expected = null;
        if (request.ExpectedLatestVersionId.ValueKind != JsonValueKind.Null)
        {
            if (request.ExpectedLatestVersionId.ValueKind != JsonValueKind.String ||
                !request.ExpectedLatestVersionId.TryGetGuid(out var predecessor) || predecessor == Guid.Empty)
                return Invalid("expectedLatestVersionId", "An explicit null or a valid version ID is required.");
            expected = new AgreementVersionId(predecessor);
        }

        if (request.Coverage is not { Length: > 0 }) return Invalid("coverage", "At least one Price List is required.");
        var groupDiscounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var submittedGroups = request.GroupDiscounts ?? [];
        for (var index = 0; index < submittedGroups.Length; index++)
        {
            var field = $"groupDiscounts[{index}]";
            var group = submittedGroups[index];
            if (group?.Code is not { Length: > 0 } code || code.Contains('\0'))
                return Invalid($"{field}.code", "Supplier Price Group code is required and cannot contain NUL.");
            if (!IsValidDiscount(group.Discount))
                return Invalid($"{field}.discount", "An explicit discount from 0 to 100 with at most one decimal place is required.");
            if (!groupDiscounts.TryAdd(code, group.Discount!.Value))
                return Invalid($"{field}.code", "Duplicate Supplier Price Group code.");
        }
        var references = new Dictionary<PriceListId, PriceListReference>();
        for (var index = 0; index < request.Coverage.Length; index++)
        {
            var field = $"coverage[{index}]";
            var item = request.Coverage[index];
            if (item is null) return Invalid(field, "Coverage is required.");
            var type = item.Type switch
            {
                "general" => PriceListType.General,
                "customerSpecificNet" => PriceListType.CustomerSpecificNet,
                _ => (PriceListType?)null,
            };
            if (type is null) return Invalid($"{field}.type", "Unsupported Price List type.");
            if (item.PriceListId == Guid.Empty) return Invalid($"{field}.priceListId", "Price List is required.");
            var listId = new PriceListId(item.PriceListId);
            if (!references.TryAdd(listId, new(listId, type.Value)))
                return Invalid($"{field}.priceListId", "Duplicate Price List.");
            if (type == PriceListType.CustomerSpecificNet && item.GeneralDiscount is not null)
                return Invalid($"{field}.generalDiscount", "Net coverage cannot carry a discount.");
            if (type == PriceListType.General && !IsValidDiscount(item.GeneralDiscount))
                return Invalid($"{field}.generalDiscount", "An explicit discount from 0 to 100 with at most one decimal place is required.");
        }
        var verification = await lists.VerifyAsync(acting, relationship.CustomerOrganizationId,
            references.Values, cancellationToken);
        if (verification is PriceListVerificationResult.Invalid invalid)
        {
            var invalidIds = invalid.References.Select(reference => reference.PriceListId.Value).ToHashSet();
            var errors = new Dictionary<string, string[]>();
            for (var index = 0; index < request.Coverage.Length; index++)
                if (invalidIds.Contains(request.Coverage[index].PriceListId))
                    errors.Add($"coverage[{index}].priceListId", ["Invalid Price List scope."]);
            return Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
        }
        if (verification is PriceListVerificationResult.Unavailable)
            return Results.Problem(statusCode: 503, extensions: new Dictionary<string, object?>
                { ["code"] = "agreement_list_verification_unavailable", ["retryable"] = true });

        var normalized = Normalize(expected?.Value, request.ValidFrom.Value, request.ValidTo,
            request.Coverage.Select(item => new CoverageResponse(item.PriceListId, item.Type!, item.GeneralDiscount)),
            groupDiscounts.Select(group => new GroupDiscountResponse(group.Key, group.Value)));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var agreement = await db.PriceAgreements.FromSqlInterpolated($"""
            SELECT * FROM agreements.price_agreements WHERE "Id" = {agreementId} FOR UPDATE
            """).SingleAsync(cancellationToken);
        if (agreement.LatestVersionId != expected)
        {
            if (agreement.LatestVersionId is not { } latestId)
                return Problem(409, "agreement_publication_conflict");
            var latest = await db.Versions.AsNoTracking().SingleAsync(x => x.Id == latestId, cancellationToken);
            if (latest.PredecessorVersionId != expected ||
                (latest.NormalizedRequest is { } saved && saved != normalized))
                return Problem(409, "agreement_publication_conflict");

            var savedCoverage = await db.Coverage.AsNoTracking().Where(x => x.VersionId == latestId).ToArrayAsync(cancellationToken);
            var savedGroups = await db.GroupDiscounts.AsNoTracking().Where(x => x.VersionId == latestId).ToArrayAsync(cancellationToken);
            var response = VersionResponse.From(latest, savedCoverage, savedGroups);
            // Versions published before this migration carry the same inputs in their immutable terms.
            if (latest.NormalizedRequest is null &&
                Normalize(response.PredecessorVersionId, response.ValidFrom, response.ValidTo, response.Coverage, response.GroupDiscounts) != normalized)
                return Problem(409, "agreement_publication_conflict");

            return Results.Ok(response);
        }
        var version = PriceAgreementVersion.Create(agreement, request.ValidFrom.Value, request.ValidTo, person.PersonId,
            clock.GetUtcNow(), rounding, normalized);
        var coverage = request.Coverage.Select(item =>
            AgreementVersionCoverage.Create(version.Id, new(item.PriceListId), item.Type!, item.GeneralDiscount)).ToArray();
        var groups = groupDiscounts.Select(group =>
            AgreementVersionGroupDiscount.Create(version.Id, group.Key, group.Value)).ToArray();
        db.Versions.Add(version);
        db.GroupDiscounts.AddRange(groups);
        db.Coverage.AddRange(coverage);
        // Insert the version before moving the constrained latest pointer, within the same local transaction.
        await db.SaveChangesAsync(cancellationToken);
        agreement.Publish(version);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/agreements/{agreement.Id}/versions/{version.Id}", VersionResponse.From(version, coverage, groups));
    }

    private static string Normalize(Guid? predecessor, DateOnly validFrom, DateOnly? validTo,
        IEnumerable<CoverageResponse> coverage, IEnumerable<GroupDiscountResponse> groups) =>
        JsonSerializer.Serialize(new
        {
            predecessor, validFrom, validTo,
            coverage = coverage.OrderBy(item => item.PriceListId).Select(item => new
            {
                item.PriceListId, item.Type,
                discountTenths = item.GeneralDiscount is { } discount ? (int?)(discount * 10) : null,
            }),
            groups = groups.OrderBy(group => group.Code, StringComparer.Ordinal).Select(group => new
            {
                group.Code, discountTenths = (int)(group.Discount * 10),
            }),
        });

    private static bool IsValidDiscount(decimal? value) =>
        value is >= 0 and <= 100 && decimal.Round(value.Value, 1) == value.Value;

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static IResult Invalid(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] },
        extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
}
