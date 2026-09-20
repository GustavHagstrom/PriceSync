using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships;

public static class TradingRelationshipsModuleServiceCollectionExtensions
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

        //services.AddScoped<GetTradingRelationshipHandler>();
        //services.AddScoped<ListMyTradingRelationshipsHandler>();

        return services;
    }
}
