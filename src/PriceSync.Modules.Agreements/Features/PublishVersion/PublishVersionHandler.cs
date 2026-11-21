using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed class PublishVersionHandler(AgreementsDbContext db, TimeProvider clock, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
    IPriceListScopeVerifier lists, AgreementRoundingPolicy rounding)
{
    internal async Task<PublishVersionResult> HandleAsync(PublishVersionCommand command, CancellationToken cancellationToken)
    {
        var agreementId = command.AgreementId;
        if (organization.OrganizationId is not { } acting) return new PublishVersionResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return new PublishVersionResult.Forbidden();
        if (agreementId == Guid.Empty) return new PublishVersionResult.NotFound();
        var id = new AgreementId(agreementId);
        var scope = await db.PriceAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (scope is null) return new PublishVersionResult.NotFound();
        var relationship = await relationships.GetAsync(scope.TradingRelationshipId, cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting) return new PublishVersionResult.NotFound();
        if (relationship.Status == TradingRelationshipStatus.Ended) return new PublishVersionResult.RelationshipEnded();
        if (command.ValidFrom is null) return Invalid("validFrom", "ValidFrom is required.");
        if (command.ValidTo < command.ValidFrom) return Invalid("validTo", "ValidTo must be on or after ValidFrom.");
        AgreementVersionId? expected = null;
        if (command.ExpectedLatestVersionId.ValueKind != JsonValueKind.Null)
        {
            if (command.ExpectedLatestVersionId.ValueKind != JsonValueKind.String ||
                !command.ExpectedLatestVersionId.TryGetGuid(out var predecessor) || predecessor == Guid.Empty)
                return Invalid("expectedLatestVersionId", "An explicit null or a valid version ID is required.");
            expected = new AgreementVersionId(predecessor);
        }

        if (command.Coverage is not { Length: > 0 }) return Invalid("coverage", "At least one Price List is required.");
        var groupDiscounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var submittedGroups = command.GroupDiscounts ?? [];
        for (var index = 0; index < submittedGroups.Length; index++)
        {
            var field = $"groupDiscounts[{index}]";
            var group = submittedGroups[index];
            if (group?.Code is not { Length: > 0 } code || code.Contains('\0'))
                return Invalid($"{field}.code", "Supplier Price Group code is required and cannot contain NUL.");
            if (!PublicationDiscount.TryRead(group.Discount, out var discount))
                return Invalid($"{field}.discount", "An explicit discount from 0 to 100 with at most one decimal place is required.");
            if (!groupDiscounts.TryAdd(code, discount))
                return Invalid($"{field}.code", "Duplicate Supplier Price Group code.");
        }
        var coverageTerms = new List<CoverageResponse>();
        var references = new Dictionary<PriceListId, PriceListReference>();
        for (var index = 0; index < command.Coverage.Length; index++)
        {
            var field = $"coverage[{index}]";
            var item = command.Coverage[index];
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
            decimal? generalDiscount = null;
            if (type == PriceListType.General)
            {
                if (!PublicationDiscount.TryRead(item.GeneralDiscount, out var discount))
                    return Invalid($"{field}.generalDiscount", "An explicit discount from 0 to 100 with at most one decimal place is required.");
                generalDiscount = discount;
            }
            else if (item.GeneralDiscount.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                return Invalid($"{field}.generalDiscount", "Net coverage cannot carry a discount.");

            coverageTerms.Add(new(item.PriceListId, item.Type!, generalDiscount));
        }
        var verification = await lists.VerifyAsync(acting, relationship.CustomerOrganizationId,
            references.Values, cancellationToken);
        if (verification is PriceListVerificationResult.Invalid invalid)
        {
            var invalidIds = invalid.References.Select(reference => reference.PriceListId.Value).ToHashSet();
            var errors = new Dictionary<string, string[]>();
            for (var index = 0; index < command.Coverage.Length; index++)
                if (invalidIds.Contains(command.Coverage[index].PriceListId))
                    errors.Add($"coverage[{index}].priceListId", ["Invalid Price List scope."]);
            return new PublishVersionResult.ValidationFailed(errors);
        }
        if (verification is PriceListVerificationResult.Unavailable)
            return new PublishVersionResult.ListVerificationUnavailable();

        var normalized = Normalize(expected?.Value, command.ValidFrom.Value, command.ValidTo,
            coverageTerms,
            groupDiscounts.Select(group => new GroupDiscountResponse(group.Key, group.Value)));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var agreement = await db.PriceAgreements.FromSqlInterpolated($"""
            SELECT * FROM agreements.price_agreements WHERE "Id" = {agreementId} FOR UPDATE
            """).SingleAsync(cancellationToken);
        if (agreement.LatestVersionId != expected)
        {
            if (agreement.LatestVersionId is not { } latestId)
                return new PublishVersionResult.PublicationConflict();
            var latest = await db.Versions.AsNoTracking().SingleAsync(x => x.Id == latestId, cancellationToken);
            if (latest.PredecessorVersionId != expected ||
                (latest.NormalizedRequest is { } saved && saved != normalized))
                return new PublishVersionResult.PublicationConflict();

            var savedCoverage = await db.Coverage.AsNoTracking().Where(x => x.VersionId == latestId).ToArrayAsync(cancellationToken);
            var savedGroups = await db.GroupDiscounts.AsNoTracking().Where(x => x.VersionId == latestId).ToArrayAsync(cancellationToken);
            var response = VersionResponse.From(latest, savedCoverage, savedGroups);
            // Versions published before this migration carry the same inputs in their immutable terms.
            if (latest.NormalizedRequest is null &&
                Normalize(response.PredecessorVersionId, response.ValidFrom, response.ValidTo, response.Coverage, response.GroupDiscounts) != normalized)
                return new PublishVersionResult.PublicationConflict();

            return new PublishVersionResult.Existing(response);
        }
        var version = PriceAgreementVersion.Create(agreement, command.ValidFrom.Value, command.ValidTo, person.PersonId,
            clock.GetUtcNow(), rounding, normalized);
        var coverage = coverageTerms.Select(item =>
            AgreementVersionCoverage.Create(version.Id, new(item.PriceListId), item.Type, item.GeneralDiscount)).ToArray();
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
        return new PublishVersionResult.Created($"/api/v1/agreements/{agreement.Id}/versions/{version.Id}", VersionResponse.From(version, coverage, groups));
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

    private static PublishVersionResult Invalid(string field, string message) =>
        new PublishVersionResult.ValidationFailed(new Dictionary<string, string[]> { [field] = [message] });
}
