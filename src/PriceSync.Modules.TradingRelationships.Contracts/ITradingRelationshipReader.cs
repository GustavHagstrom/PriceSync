using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.Contracts;

/// <summary>
/// Reads the parties and current status of a Trading Relationship. Callers must authorize the acting
/// Organization and verify that it is one of the returned parties.
/// Status is observed at query time; technical failures propagate to the caller.
/// </summary>
public interface ITradingRelationshipReader
{
    Task<TradingRelationshipDetails?> GetAsync(TradingRelationshipId relationshipId, CancellationToken cancellationToken);
}

public sealed record TradingRelationshipDetails(
    OrganizationId SupplierOrganizationId,
    OrganizationId CustomerOrganizationId,
    TradingRelationshipStatus Status);

public enum TradingRelationshipStatus
{
    Active,
    Ended,
}
