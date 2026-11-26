using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementAcceptanceStore(AgreementsDbContext db, TimeProvider clock)
{
    private Task<AgreementAcceptance?> GetAsync(TradingRelationshipId relationshipId,
        AgreementVersionId versionId, CancellationToken cancellationToken) =>
        db.Set<AgreementAcceptance>().AsNoTracking().SingleOrDefaultAsync(
            x => x.TradingRelationshipId == relationshipId && x.VersionId == versionId, cancellationToken);

    // A caller can include this operation in its current Agreements transaction.
    internal async Task<AgreementAcceptance> AcceptAsync(TradingRelationshipId relationshipId,
        AgreementVersionId versionId, PersonId personId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.agreement_acceptances
                ("TradingRelationshipId", "VersionId", "AcceptedByPersonId", "AcceptedAt")
            VALUES ({relationshipId.Value}, {versionId.Value}, {personId.Value}, {clock.GetUtcNow()})
            ON CONFLICT ("TradingRelationshipId", "VersionId") DO NOTHING
            """, cancellationToken);

        return await GetAsync(relationshipId, versionId, cancellationToken)
            ?? throw new InvalidOperationException("The agreement acceptance could not be read after insertion.");
    }
}
