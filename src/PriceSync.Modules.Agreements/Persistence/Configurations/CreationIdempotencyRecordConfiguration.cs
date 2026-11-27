using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class CreationIdempotencyRecordConfiguration : IEntityTypeConfiguration<CreationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<CreationIdempotencyRecord> builder)
    {
        builder.ToTable("creation_idempotency_records");
        builder.HasKey(x => new { x.PersonId, x.ActingOrganizationId, x.Operation, x.Key })
            .HasName("pk_creation_idempotency_records");
        builder.Property(x => x.PersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.Property(x => x.ActingOrganizationId).HasConversion(x => x.Value, x => new OrganizationId(x));
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        builder.Property(x => x.Operation).HasMaxLength(100).UseCollation("C");
        builder.Property(x => x.Key).HasMaxLength(200).UseCollation("C");
    }
}
