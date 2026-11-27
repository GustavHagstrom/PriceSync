using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementsDbContextFactory : IDesignTimeDbContextFactory<AgreementsDbContext>
{
    public AgreementsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRICESYNC_AGREEMENTS_DESIGN_TIME_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=pricesync_design_time;Username=pricesync";
        return new AgreementsDbContext(new DbContextOptionsBuilder<AgreementsDbContext>().UseNpgsql(connectionString,
            options => options.MigrationsHistoryTable(AgreementsDbContext.MigrationsHistoryTableName,
                AgreementsDbContext.SchemaName)).Options);
    }
}
