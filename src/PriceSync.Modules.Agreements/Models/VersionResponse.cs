using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Models;

internal sealed record VersionResponse(Guid Id, Guid AgreementId, Guid TradingRelationshipId,
    Guid? PredecessorVersionId, long Ordinal, DateOnly ValidFrom, DateOnly? ValidTo,
    DateTimeOffset PublishedAt, Guid PublishedByPersonId, CoverageResponse[] Coverage, RoundingResponse Rounding, GroupDiscountResponse[] GroupDiscounts)
{
    internal static VersionResponse From(PriceAgreementVersion version, IEnumerable<AgreementVersionCoverage> coverage, IEnumerable<AgreementVersionGroupDiscount> groups) => new(
        version.Id.Value, version.AgreementId.Value, version.TradingRelationshipId.Value, null, version.Ordinal,
        version.ValidFrom, version.ValidTo, version.PublishedAt, version.PublishedByPersonId.Value,
        coverage.OrderBy(item => item.PriceListId.Value).Select(item => new CoverageResponse(item.PriceListId.Value, item.Type, item.GeneralDiscount)).ToArray(),
        new(version.DecimalPlaces, version.Midpoint, version.RoundingStage, version.PreservePriceQuantityAndUnitOfMeasure),
        groups.OrderBy(group => group.Code, StringComparer.Ordinal).Select(group => new GroupDiscountResponse(group.Code, group.Discount)).ToArray());
}

internal sealed record CoverageResponse(Guid PriceListId, string Type, decimal? GeneralDiscount);

internal sealed record RoundingResponse(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure);

internal sealed record GroupDiscountResponse(string Code, decimal Discount);
