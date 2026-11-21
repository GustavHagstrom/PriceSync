using PriceSync.Modules.Agreements.Contracts;

namespace PriceSync.Modules.Agreements.Domain;

internal sealed class AgreementVersionGroupDiscount
{
    private AgreementVersionGroupDiscount() { }
    public AgreementVersionId VersionId { get; private set; }
    public string Code { get; private set; } = null!;
    public decimal Discount { get; private set; }

    internal static AgreementVersionGroupDiscount Create(AgreementVersionId version, string code, decimal discount) =>
        new() { VersionId = version, Code = code, Discount = discount };
}
