using TradingRelationshipStatus = PriceSync.Modules.TradingRelationships.Domain.TradingRelationshipStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;

namespace PriceSync.Modules.TradingRelationships.Persistence.Configurations;

internal class TradingRelationshipConfiguration : IEntityTypeConfiguration<TradingRelationship>
{
    internal const string ActiveSupplierCustomerUniqueIndex = "ux_trading_relationships_active_supplier_customer";
    public void Configure(EntityTypeBuilder<TradingRelationship> builder)
    {
        builder.ToTable("TradingRelationships", TradingRelationshipsDbContext.SchemaName);

        builder.HasKey(tr => tr.Id)
            .HasName("pk_trading_relationships");

        builder.Property(tr => tr.Id)
            .HasColumnName("Id")
            .HasConversion(trId => trId.Value, value => TradingRelationshipId.FromGuid(value))
            .ValueGeneratedNever();

        builder.Property(tr => tr.SupplierOrganizationId)
            .HasColumnName("supplier_organization_id")
            .HasConversion(orgId => orgId.Value, value => new OrganizationId(value))
            .IsRequired();

        builder.Property(tr => tr.CustomerOrganizationId)
            .HasColumnName("customer_organization_id")
            .HasConversion(orgId => orgId.Value, value => new OrganizationId(value))
            .IsRequired();

        builder.Property(tr => tr.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(tr => tr.EndedAt)
            .HasColumnName("ended_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(tr => tr.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<TradingRelationshipStatus>(value, ignoreCase: true))
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(relationship => new
        {
            relationship.SupplierOrganizationId,
            relationship.Status,
            relationship.CreatedAt,
            relationship.Id
        })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("ix_trading_relationships_supplier_status_created_at_id");

        builder.HasIndex(relationship => new
        {
            relationship.CustomerOrganizationId,
            relationship.Status,
            relationship.CreatedAt,
            relationship.Id
        })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("ix_trading_relationships_customer_status_created_at_id");

        builder.HasIndex(relationship => new
        {
            relationship.SupplierOrganizationId,
            relationship.CustomerOrganizationId,
        })
            .IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName(ActiveSupplierCustomerUniqueIndex);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_trading_relationships_status",
                "status in ('active', 'ended')");

            table.HasCheckConstraint(
                "ck_trading_relationships_status_ended_at",
                """
                (status = 'active' and ended_at is null)
                or
                (status = 'ended' and ended_at is not null)
                """);

            table.HasCheckConstraint(
                "ck_trading_relationships_distinct_organizations",
                "supplier_organization_id <> customer_organization_id");
        });
    }
}
