using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Application;

public static class PriceSyncApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPriceSyncApplication(this IServiceCollection services)
    {
        return services;
    }
}
