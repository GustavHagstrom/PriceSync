namespace PriceSync.Modules.PricePublication.Contracts;

public sealed record PriceListReference(PriceListId PriceListId, PriceListType ExpectedType);
