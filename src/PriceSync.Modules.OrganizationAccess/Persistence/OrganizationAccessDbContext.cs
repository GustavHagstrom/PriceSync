using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Domain;

namespace PriceSync.Modules.OrganizationAccess.Persistence;

internal sealed class OrganizationAccessDbContext(DbContextOptions<OrganizationAccessDbContext> options) : DbContext(options)
{
    public const string SchemaName = "organization_access";
    public const string MigrationsHistoryTableName = "__EFMigrationsHistory";

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationAccessDbContext).Assembly);
    }
}
