using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceSync.Modules.OrganizationAccess.Persistence;

internal sealed class OrganizationAccessDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationAccessDbContext>
{
    public OrganizationAccessDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationAccessDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=pricesync_design_time;Username=pricesync;Password=pricesync",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                OrganizationAccessDbContext.MigrationsHistoryTableName,
                OrganizationAccessDbContext.SchemaName));

        return new OrganizationAccessDbContext(optionsBuilder.Options);
    }
}
