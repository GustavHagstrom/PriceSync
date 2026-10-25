using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.PricePublication.Persistence;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.PricePublication.Features.Verification;

namespace PriceSync.Modules.PricePublication;

public static class DependencyInjection
{
    public static IServiceCollection AddPricePublicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PricePublication")
            ?? throw new InvalidOperationException("Connection string 'PricePublication' is required.");
        services.AddDbContext<PricePublicationDbContext>(options => options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable(PricePublicationDbContext.MigrationsHistoryTableName,
                PricePublicationDbContext.SchemaName)));
        services.AddScoped<IPriceListScopeVerifier, PriceListScopeVerifier>();
        return services;
    }
}
