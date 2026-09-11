using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess;

public static class OrganizationAccessModuleServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationAccessModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrganizationAccess")
            ?? throw new InvalidOperationException("Connection string 'OrganizationAccess' is required.");

        return services.AddOrganizationAccessModule(connectionString);
    }

    public static IServiceCollection AddOrganizationAccessModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<OrganizationAccessDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    OrganizationAccessDbContext.MigrationsHistoryTableName,
                    OrganizationAccessDbContext.SchemaName));
        });

        return services;
    }
}
