using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.Features.AgreementScope;

internal sealed class AgreementScopeReader(TradingRelationshipsDbContext dbContext) : IAgreementScopeReader
{
    public Task<Contracts.AgreementScope?> GetAsync(
        TradingRelationshipId relationshipId, CancellationToken cancellationToken) =>
        dbContext.TradingRelationships.AsNoTracking()
            .Where(relationship => relationship.Id == relationshipId)
            .Select(relationship => new Contracts.AgreementScope(
                relationship.SupplierOrganizationId,
                relationship.CustomerOrganizationId,
                relationship.Status == TradingRelationshipStatus.Active
                    ? AgreementScopeStatus.Active : AgreementScopeStatus.Ended))
            .SingleOrDefaultAsync(cancellationToken);
}
