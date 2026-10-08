using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Features.Authorization;
using PriceSync.Modules.OrganizationAccess.Features.GetOrganization;
using PriceSync.Modules.OrganizationAccess.Features.ListMyOrganizations;
using PriceSync.Modules.OrganizationAccess.Features.Status;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess;

public static class OrganizationAccessModuleServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationAccessModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrganizationAccess")
            ?? throw new InvalidOperationException("Connection string 'OrganizationAccess' is required.");

        services.AddDbContext<OrganizationAccessDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    OrganizationAccessDbContext.MigrationsHistoryTableName,
                    OrganizationAccessDbContext.SchemaName));
        });

        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<ListMyOrganizationsHandler>();
        services.AddScoped<IOrganizationAuthorizer, OrganizationAuthorizer>();
        services.AddScoped<IOrganizationStatusReader, OrganizationStatusReader>();

        return services;
    }
}
