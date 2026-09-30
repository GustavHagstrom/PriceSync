using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.TradingRelationships.Features.Create;
using PriceSync.Modules.TradingRelationships.Features.End;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingRelationshipsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TradingRelationships")
            ?? throw new InvalidOperationException("Connection string 'TradingRelationships' is required.");

        services.AddDbContext<TradingRelationshipsDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    TradingRelationshipsDbContext.MigrationsHistoryTableName,
                    TradingRelationshipsDbContext.SchemaName));
        });

        services.AddScoped<CreateHandler>();
        services.AddScoped<CreateValidator>();

        services.AddScoped<EndHandler>();
        services.AddScoped<EndValidator>();

        return services;
    }
}
