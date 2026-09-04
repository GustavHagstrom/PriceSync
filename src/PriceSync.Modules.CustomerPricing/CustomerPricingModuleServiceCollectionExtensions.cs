using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Modules.CustomerPricing;

public static class CustomerPricingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerPricingModule(this IServiceCollection services)
    {
        return services;
    }
}
