using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Models;

internal sealed record VersionResponse(Guid Id, Guid AgreementId, Guid TradingRelationshipId,
    Guid? PredecessorVersionId, long Ordinal, DateOnly ValidFrom, DateOnly? ValidTo,
    DateTimeOffset PublishedAt, Guid PublishedByPersonId, CoverageResponse[] Coverage, RoundingResponse Rounding, GroupDiscountResponse[] GroupDiscounts)
{
    public WithdrawalResponse? Withdrawal { get; init; }
    public string[] Warnings { get; init; } = [];

    internal static VersionResponse From(PriceAgreementVersion version, IEnumerable<AgreementVersionCoverage> coverage,
        IEnumerable<AgreementVersionGroupDiscount> groups, DateOnly date) => From(AgreementVersionFactsMapper.From(version, coverage, groups), date);

    internal static VersionResponse From(AgreementVersionFacts facts, DateOnly date) => new(
        facts.Id.Value, facts.AgreementId.Value, facts.TradingRelationshipId.Value, facts.PredecessorVersionId?.Value, facts.Ordinal,
        facts.ValidFrom, facts.ValidTo, facts.PublishedAt, facts.PublishedByPersonId.Value,
        facts.Coverage.Select(item => new CoverageResponse(item.PriceListId.Value,
            CoverageType.Format(item.Type), item.GeneralDiscount)).ToArray(),
        new(facts.Rounding.DecimalPlaces, facts.Rounding.Midpoint, facts.Rounding.Stage, facts.Rounding.PreservePriceQuantityAndUnitOfMeasure),
        facts.GroupDiscounts.Select(group => new GroupDiscountResponse(group.Code, group.Discount)).ToArray())
        {
            Warnings = GetWarnings(facts, date),
            Withdrawal = facts.Withdrawal is { } withdrawal
                ? new(withdrawal.Reason, withdrawal.WithdrawnByPersonId.Value, withdrawal.WithdrawnAt)
                : null,
        };

    private static string[] GetWarnings(AgreementVersionFacts facts, DateOnly date)
    {
        var warnings = new List<string>(2);
        if (facts.Withdrawal is not null) warnings.Add("agreement_version_withdrawn");
        if (facts.IsOutsideValidityOn(date)) warnings.Add("agreement_version_outside_validity");
        return warnings.ToArray();
    }
}

internal sealed record WithdrawalResponse(string Reason, Guid WithdrawnByPersonId, DateTimeOffset WithdrawnAt);

internal sealed record CoverageResponse(Guid PriceListId, string Type, decimal? GeneralDiscount);

internal sealed record RoundingResponse(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure);

internal sealed record GroupDiscountResponse(string Code, decimal Discount);
