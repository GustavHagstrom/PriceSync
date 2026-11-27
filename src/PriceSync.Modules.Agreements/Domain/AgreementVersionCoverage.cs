using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed class AgreementVersionCoverage
{
    private AgreementVersionCoverage() { }
    public AgreementVersionId VersionId { get; private set; }
    public PriceListId PriceListId { get; private set; }
    public PriceListType Type { get; private set; }
    public decimal? GeneralDiscount { get; private set; }

    internal static AgreementVersionCoverage Create(AgreementVersionId version, PriceListId list, PriceListType type, decimal? discount) =>
        new() { VersionId = version, PriceListId = list, Type = type, GeneralDiscount = discount };
}
