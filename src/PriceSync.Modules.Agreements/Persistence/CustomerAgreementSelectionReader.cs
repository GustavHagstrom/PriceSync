using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class CustomerAgreementSelectionReader(AgreementsDbContext db) : ICustomerAgreementSelectionReader
{
    public Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        CancellationToken cancellationToken = default) =>
        db.Set<CustomerAgreementState>()
            .Where(state => state.TradingRelationshipId == relationshipId)
            .Select(state => state.ActiveVersionId)
            .SingleOrDefaultAsync(cancellationToken);
}
