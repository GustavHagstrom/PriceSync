using Microsoft.Extensions.DependencyInjection;

namespace PriceSync.Modules.OrganizationAccess;

public static class OrganizationAccessModuleServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationAccessModule(this IServiceCollection services)
    {
        return services;
    }
}
