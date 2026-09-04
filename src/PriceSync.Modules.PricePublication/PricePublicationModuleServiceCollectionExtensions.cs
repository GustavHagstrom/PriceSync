using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Modules.PricePublication;

public static class PricePublicationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPricePublicationModule(this IServiceCollection services)
    {
        return services;
    }
}
