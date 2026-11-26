using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Recommendation;

internal sealed class RecommendationHandler(AgreementsDbContext db, AgreementVersionReader reader,
    ICurrentPerson person, ICurrentOrganization organization, IOrganizationAuthorizer authorizer,
    ITradingRelationshipReader relationships)
{
    internal async Task<RecommendationResult> HandleAsync(RecommendationQuery query, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return new RecommendationResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return new RecommendationResult.Forbidden();
        if (query.AgreementId == Guid.Empty) return new RecommendationResult.NotFound();

        var id = new AgreementId(query.AgreementId);
        var agreement = await db.PriceAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (agreement is null) return new RecommendationResult.NotFound();
        var relationship = await relationships.GetAsync(agreement.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return new RecommendationResult.NotFound();

        var version = await db.Versions.AsNoTracking()
            .Where(x => x.AgreementId == id && x.PublishedAt <= query.KnowledgeCutoff
                && x.ValidFrom <= query.EvaluatedDate && (x.ValidTo == null || x.ValidTo >= query.EvaluatedDate)
                && !db.Withdrawals.Any(w => w.VersionId == x.Id && w.WithdrawnAt <= query.KnowledgeCutoff))
            .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.Ordinal)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null) return new RecommendationResult.Success(null);
        var facts = await reader.ReadFactsAsync(version, cancellationToken);
        return new RecommendationResult.Success(VersionResponse.From(facts with { Withdrawal = null }, query.EvaluatedDate));
    }
}
