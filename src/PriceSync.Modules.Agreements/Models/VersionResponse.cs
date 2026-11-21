using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Models;

internal sealed record VersionResponse(Guid Id, Guid AgreementId, Guid TradingRelationshipId,
    Guid? PredecessorVersionId, long Ordinal, DateOnly ValidFrom, DateOnly? ValidTo,
    DateTimeOffset PublishedAt, Guid PublishedByPersonId, CoverageResponse[] Coverage, RoundingResponse Rounding, GroupDiscountResponse[] GroupDiscounts)
{
    internal static VersionResponse From(PriceAgreementVersion version, IEnumerable<AgreementVersionCoverage> coverage,
        IEnumerable<AgreementVersionGroupDiscount> groups) => From(AgreementVersionFactsMapper.From(version, coverage, groups));

    internal static VersionResponse From(AgreementVersionFacts facts) => new(
        facts.Id.Value, facts.AgreementId.Value, facts.TradingRelationshipId.Value, facts.PredecessorVersionId?.Value, facts.Ordinal,
        facts.ValidFrom, facts.ValidTo, facts.PublishedAt, facts.PublishedByPersonId.Value,
        facts.Coverage.Select(item => new CoverageResponse(item.PriceListId.Value, item.Type switch
        {
            PriceListType.General => "general",
            PriceListType.CustomerSpecificNet => "customerSpecificNet",
            _ => throw new InvalidOperationException("Unsupported Price List type."),
        }, item.GeneralDiscount)).ToArray(),
        new(facts.Rounding.DecimalPlaces, facts.Rounding.Midpoint, facts.Rounding.Stage, facts.Rounding.PreservePriceQuantityAndUnitOfMeasure),
        facts.GroupDiscounts.Select(group => new GroupDiscountResponse(group.Code, group.Discount)).ToArray());
}

internal sealed record CoverageResponse(Guid PriceListId, string Type, decimal? GeneralDiscount);

internal sealed record RoundingResponse(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure);

internal sealed record GroupDiscountResponse(string Code, decimal Discount);
