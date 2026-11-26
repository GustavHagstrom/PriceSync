using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Acceptance;

internal sealed class AcceptanceScopeReader(AgreementsDbContext db, ITradingRelationshipReader relationships)
{
    internal async Task<(TradingRelationshipId RelationshipId, AgreementVersionId VersionId, TradingRelationshipDetails Details)?> FindAsync(
        Guid agreementId, Guid versionId, CancellationToken cancellationToken)
    {
        if (AcceptanceValidator.Validate(agreementId, versionId) is not { } ids)
            return null;

        var relationshipId = await db.Versions
            .Where(x => x.Id == ids.VersionId && x.AgreementId == ids.AgreementId)
            .Select(x => (TradingRelationshipId?)x.TradingRelationshipId)
            .SingleOrDefaultAsync(cancellationToken);
        if (relationshipId is not { } scope)
            return null;

        var details = await relationships.GetAsync(scope, cancellationToken);
        return details is null ? null : (scope, ids.VersionId, details);
    }
}
