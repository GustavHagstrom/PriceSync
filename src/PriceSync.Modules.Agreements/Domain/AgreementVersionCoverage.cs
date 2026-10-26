using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed class AgreementVersionCoverage
{
    private AgreementVersionCoverage() { }
    public AgreementVersionId VersionId { get; private set; }
    public PriceListId PriceListId { get; private set; }
    public string Type { get; private set; } = null!;
    public decimal GeneralDiscount { get; private set; }

    internal static AgreementVersionCoverage General(AgreementVersionId version, PriceListId list, decimal discount) =>
        new() { VersionId = version, PriceListId = list, Type = "general", GeneralDiscount = discount };
}
