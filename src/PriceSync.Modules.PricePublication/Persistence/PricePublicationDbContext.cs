using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.PricePublication.Contracts;
using PriceSync.Modules.PricePublication.Domain;

namespace PriceSync.Modules.PricePublication.Persistence;

internal sealed class PricePublicationDbContext(DbContextOptions<PricePublicationDbContext> options) : DbContext(options)
{
    public const string SchemaName = "price_publication";
    public const string MigrationsHistoryTableName = "__EFMigrationsHistory";
    public DbSet<PriceList> PriceLists => Set<PriceList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        var list = modelBuilder.Entity<PriceList>();
        list.ToTable("price_lists");
        list.HasKey(x => x.Id);
        list.Property(x => x.Id).HasConversion(x => x.Value, x => new PriceListId(x)).ValueGeneratedNever();
        list.Property(x => x.OwnerOrganizationId).HasConversion(x => x.Value, x => new OrganizationId(x));
        list.Property(x => x.CustomerOrganizationId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null,
            x => x.HasValue ? new OrganizationId(x.Value) : null);
        list.Property(x => x.Name).HasMaxLength(200).IsRequired();
        list.Property(x => x.Type).HasMaxLength(40).IsRequired();
        list.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        list.Property(x => x.Market).HasMaxLength(100).IsRequired();
        foreach (var property in new[] { nameof(PriceList.OwnerOrganizationId), nameof(PriceList.Type),
                     nameof(PriceList.Currency), nameof(PriceList.Market), nameof(PriceList.CustomerOrganizationId) })
            list.Property(property).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
