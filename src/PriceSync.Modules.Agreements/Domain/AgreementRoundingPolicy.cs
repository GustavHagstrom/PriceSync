namespace PriceSync.Modules.Agreements.Domain;

internal sealed record AgreementRoundingPolicy(int DecimalPlaces, string Midpoint, string Stage,
    bool PreservePriceQuantityAndUnitOfMeasure)
{
    internal static AgreementRoundingPolicy Default { get; } =
        new(4, "AwayFromZero", "OnceAfterDiscountWithoutIntermediateRounding", true);
}
