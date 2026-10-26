using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceSync.Modules.PricePublication.Persistence;

internal sealed class PricePublicationDbContextFactory
    : IDesignTimeDbContextFactory<PricePublicationDbContext>
{
    public PricePublicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "PRICESYNC_PRICE_PUBLICATION_DESIGN_TIME_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=pricesync_design_time;Username=pricesync";

        var options = new DbContextOptionsBuilder<PricePublicationDbContext>();

        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                PricePublicationDbContext.MigrationsHistoryTableName,
                PricePublicationDbContext.SchemaName));

        return new PricePublicationDbContext(options.Options);
    }
}
