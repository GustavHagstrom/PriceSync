using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Contracts;

/// <summary>Reads the current agreement selection for trusted module consumers.</summary>
public interface ICustomerAgreementSelectionReader
{
    /// <summary>Returns the exact active version ID, or null when there is no selection. Technical failures propagate.</summary>
    Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        CancellationToken cancellationToken = default);
}
