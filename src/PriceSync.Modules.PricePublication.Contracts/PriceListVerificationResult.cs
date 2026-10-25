namespace PriceSync.Modules.PricePublication.Contracts;

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
