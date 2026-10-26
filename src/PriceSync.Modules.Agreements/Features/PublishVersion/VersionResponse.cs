using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Features.PublishVersion;

internal sealed record VersionResponse(Guid Id, Guid AgreementId, Guid TradingRelationshipId,
    Guid? PredecessorVersionId, long Ordinal, DateOnly ValidFrom, DateOnly? ValidTo,
    DateTimeOffset PublishedAt, Guid PublishedByPersonId, CoverageRequest[] Coverage, RoundingResponse Rounding)
{
    internal static VersionResponse From(PriceAgreementVersion version, AgreementVersionCoverage coverage) => new(
        version.Id.Value, version.AgreementId.Value, version.TradingRelationshipId.Value, null, version.Ordinal,
        version.ValidFrom, version.ValidTo, version.PublishedAt, version.PublishedByPersonId.Value,
        [new(coverage.PriceListId.Value, coverage.Type, coverage.GeneralDiscount)],
        new(version.DecimalPlaces, version.Midpoint, version.RoundingStage, version.PreservePriceQuantityAndUnitOfMeasure));
}

internal sealed record RoundingResponse(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure);
