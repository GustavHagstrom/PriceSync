using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.Features.ListCustomer;

internal abstract record ListCustomerResult
{
    public record Success(ListCustomerResponse Response) : ListCustomerResult;
    public record ActingOrganizationMissing : ListCustomerResult;
    public record NotCapable : ListCustomerResult;
}
