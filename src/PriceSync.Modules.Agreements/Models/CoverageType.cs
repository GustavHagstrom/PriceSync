using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Models;

internal static class CoverageType
{
    internal static PriceListType? Parse(string? value) => value switch
    {
        "general" => PriceListType.General,
        "customerSpecificNet" => PriceListType.CustomerSpecificNet,
        _ => null,
    };

    internal static string Format(PriceListType type) => type switch
    {
        PriceListType.General => "general",
        PriceListType.CustomerSpecificNet => "customerSpecificNet",
        _ => throw new InvalidOperationException("Unsupported Price List type."),
    };
}
