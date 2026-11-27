using System.Collections.Immutable;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Contracts;

/// <summary>Reads immutable version facts for trusted module consumers.</summary>
public interface IAgreementVersionReader
{
    /// <summary>Returns null when the exact version does not exist. Technical failures propagate.</summary>
    Task<AgreementVersionFacts?> GetAsync(AgreementVersionId versionId, CancellationToken cancellationToken = default);
}

public sealed record AgreementVersionFacts(AgreementVersionId Id, AgreementId AgreementId,
    TradingRelationshipId TradingRelationshipId, AgreementVersionId? PredecessorVersionId, long Ordinal,
    DateOnly ValidFrom, DateOnly? ValidTo, DateTimeOffset PublishedAt, PersonId PublishedByPersonId,
    ImmutableArray<AgreementVersionListCoverage> Coverage, AgreementVersionRounding Rounding,
    ImmutableArray<AgreementVersionGroupTerms> GroupDiscounts)
{
    public AgreementVersionWithdrawalFacts? Withdrawal { get; init; }

    public bool IsOutsideValidityOn(DateOnly evaluatedDate) =>
        evaluatedDate < ValidFrom || evaluatedDate > ValidTo;
}

public sealed record AgreementVersionWithdrawalFacts(string Reason, PersonId WithdrawnByPersonId, DateTimeOffset WithdrawnAt);

public sealed record AgreementVersionListCoverage(PriceListId PriceListId, PriceListType Type, decimal? GeneralDiscount);

public sealed record AgreementVersionRounding(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure);

/// <summary>An exact Supplier Price Group code and its replacement discount for covered General Price Lists.</summary>
public sealed record AgreementVersionGroupTerms(string Code, decimal Discount);
