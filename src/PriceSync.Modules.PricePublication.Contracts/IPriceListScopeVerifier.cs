using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.PricePublication.Contracts;

/// <summary>Verifies the complete set against immutable logical scope; no publication or write locks are required.</summary>
public interface IPriceListScopeVerifier
{
    Task<PriceListVerificationResult> VerifyAsync(OrganizationId supplier, OrganizationId customer,
        IReadOnlyCollection<PriceListReference> references, CancellationToken cancellationToken = default);
}
