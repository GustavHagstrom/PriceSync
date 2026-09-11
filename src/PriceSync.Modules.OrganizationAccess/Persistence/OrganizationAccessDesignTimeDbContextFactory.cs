using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceSync.Modules.OrganizationAccess.Persistence;

internal sealed class OrganizationAccessDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationAccessDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "PRICESYNC_ORGANIZATION_ACCESS_DESIGN_TIME_CONNECTION_STRING";
    private const string LocalDesignTimeConnectionString = "Host=localhost;Port=5432;Database=pricesync_design_time;Username=pricesync";

    public OrganizationAccessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? LocalDesignTimeConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<OrganizationAccessDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                OrganizationAccessDbContext.MigrationsHistoryTableName,
                OrganizationAccessDbContext.SchemaName));

        return new OrganizationAccessDbContext(optionsBuilder.Options);
    }
}
