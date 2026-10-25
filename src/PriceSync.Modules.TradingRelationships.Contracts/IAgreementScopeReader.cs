using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.Contracts;

/// <summary>
/// Reads relationship facts for Agreements. Callers must authorize the acting
/// Organization and verify that it is one of the returned parties.
/// Status is observed at query time; technical failures propagate to the caller.
/// </summary>
public interface IAgreementScopeReader
{
    Task<AgreementScope?> GetAsync(TradingRelationshipId relationshipId, CancellationToken cancellationToken);
}

public sealed record AgreementScope(
    OrganizationId SupplierOrganizationId,
    OrganizationId CustomerOrganizationId,
    AgreementScopeStatus Status);

public enum AgreementScopeStatus
{
    Active,
    Ended,
}
