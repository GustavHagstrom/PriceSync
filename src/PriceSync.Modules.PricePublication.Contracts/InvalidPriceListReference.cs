namespace PriceSync.Modules.PricePublication.Contracts;

public sealed record InvalidPriceListReference(PriceListId PriceListId, InvalidPriceListReferenceReason Reason);
