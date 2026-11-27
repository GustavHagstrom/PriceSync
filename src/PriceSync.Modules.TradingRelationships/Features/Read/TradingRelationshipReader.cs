using TradingRelationshipStatus = PriceSync.Modules.TradingRelationships.Domain.TradingRelationshipStatus;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.Features.Read;

internal sealed class TradingRelationshipReader(TradingRelationshipsDbContext dbContext) : ITradingRelationshipReader
{
    public Task<Contracts.TradingRelationshipDetails?> GetAsync(
        TradingRelationshipId relationshipId, CancellationToken cancellationToken) =>
        dbContext.TradingRelationships.AsNoTracking()
            .Where(relationship => relationship.Id == relationshipId)
            .Select(relationship => new Contracts.TradingRelationshipDetails(
                relationship.SupplierOrganizationId,
                relationship.CustomerOrganizationId,
                relationship.Status == TradingRelationshipStatus.Active
                    ? Contracts.TradingRelationshipStatus.Active : Contracts.TradingRelationshipStatus.Ended))
            .SingleOrDefaultAsync(cancellationToken);
}
