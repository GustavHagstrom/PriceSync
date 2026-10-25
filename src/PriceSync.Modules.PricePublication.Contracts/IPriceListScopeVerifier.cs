using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.PricePublication.Contracts;

/// <summary>Verifies the complete set against immutable logical scope; no publication or write locks are required.</summary>
public interface IPriceListScopeVerifier
{
    Task<PriceListVerificationResult> VerifyAsync(OrganizationId supplier, OrganizationId customer,
        IReadOnlyCollection<PriceListReference> references, CancellationToken cancellationToken = default);
}

public enum PriceListType { General, CustomerSpecificNet }

public sealed record PriceListReference(PriceListId PriceListId, PriceListType ExpectedType);

public sealed record VerifiedPriceListScope(PriceListId PriceListId, OrganizationId OwnerOrganizationId,
    PriceListType Type, string Currency, string Market, OrganizationId? CustomerOrganizationId);

public enum InvalidPriceListReferenceReason { InvalidId, NotFound, WrongSupplier, IncompatibleType, WrongCustomer }

public sealed record InvalidPriceListReference(PriceListId PriceListId, InvalidPriceListReferenceReason Reason);

/// <summary>Only Verified supplies scopes. Invalid and Unavailable never imply partial success.</summary>
public abstract record PriceListVerificationResult
{
    private PriceListVerificationResult() { }
    public sealed record Verified(IReadOnlyList<VerifiedPriceListScope> Scopes) : PriceListVerificationResult;
    public sealed record Invalid(IReadOnlyList<InvalidPriceListReference> References) : PriceListVerificationResult;
    public sealed record Unavailable : PriceListVerificationResult
    {
        public bool Retryable => true;
    }
}
