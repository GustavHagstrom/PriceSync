using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceSync.Modules.TradingRelationships.Persistence;

internal sealed class TradingRelationshipsDbContextFactory
    : IDesignTimeDbContextFactory<TradingRelationshipsDbContext>
{
    public TradingRelationshipsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "PRICESYNC_TRADING_RELATIONSHIPS_DESIGN_TIME_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=pricesync_design_time;Username=pricesync";

        var options = new DbContextOptionsBuilder<TradingRelationshipsDbContext>();

        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                TradingRelationshipsDbContext.MigrationsHistoryTableName,
                TradingRelationshipsDbContext.SchemaName));

        return new TradingRelationshipsDbContext(options.Options);
    }
}