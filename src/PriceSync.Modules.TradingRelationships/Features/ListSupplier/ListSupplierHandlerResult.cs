using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal abstract record class ListSupplierHandlerResult
{
    public record Succes(ListSupplierResponse Response) : ListSupplierHandlerResult;
}
