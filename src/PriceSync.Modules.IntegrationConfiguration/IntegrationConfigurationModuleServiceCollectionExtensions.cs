using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Modules.IntegrationConfiguration;

public static class IntegrationConfigurationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationConfigurationModule(this IServiceCollection services)
    {
        return services;
    }
}
