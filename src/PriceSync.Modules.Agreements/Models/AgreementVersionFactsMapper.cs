using System.Collections.Immutable;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Models;

internal static class AgreementVersionFactsMapper
{
    internal static AgreementVersionFacts From(PriceAgreementVersion version,
        IEnumerable<AgreementVersionCoverage> coverage, IEnumerable<AgreementVersionGroupDiscount> groups) =>
        new(version.Id, version.AgreementId, version.TradingRelationshipId, version.PredecessorVersionId, version.Ordinal,
            version.ValidFrom, version.ValidTo, version.PublishedAt, version.PublishedByPersonId,
            coverage.OrderBy(item => item.PriceListId.Value).Select(item =>
                new AgreementVersionListCoverage(item.PriceListId, item.Type switch
                {
                    "general" => PriceListType.General,
                    "customerSpecificNet" => PriceListType.CustomerSpecificNet,
                    _ => throw new InvalidOperationException("Unsupported stored Price List type."),
                }, item.GeneralDiscount)).ToImmutableArray(),
            new(version.DecimalPlaces, version.Midpoint, version.RoundingStage, version.PreservePriceQuantityAndUnitOfMeasure),
            groups.OrderBy(group => group.Code, StringComparer.Ordinal)
                .Select(group => new AgreementVersionGroupTerms(group.Code, group.Discount)).ToImmutableArray());
}
