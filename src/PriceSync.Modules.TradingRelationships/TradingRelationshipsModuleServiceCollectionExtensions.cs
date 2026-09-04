using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Modules.TradingRelationships;

public static class TradingRelationshipsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTradingRelationshipsModule(this IServiceCollection services)
    {
        return services;
    }
}
