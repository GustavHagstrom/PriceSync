using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.Persistence;

internal class TradingRelationshipsDbContext(DbContextOptions<TradingRelationshipsDbContext> options) : DbContext(options)
{
    public const string SchemaName = "trading_relationships";
    public const string MigrationsHistoryTableName = "__EFMigrationsHistory";
    public DbSet<TradingRelationship> TradingRelationships => Set<TradingRelationship>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingRelationshipsDbContext).Assembly);
    }
}
