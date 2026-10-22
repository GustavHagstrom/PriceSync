using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal abstract record ListSupplierResult
{
    public record Success(ListSupplierResponse Response) : ListSupplierResult;
    public record ActingOrganizationMissing : ListSupplierResult;
    public record NotCapable : ListSupplierResult;
}
