using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class PriceAgreementConfiguration : IEntityTypeConfiguration<PriceAgreement>
{
    public void Configure(EntityTypeBuilder<PriceAgreement> builder)
    {
        builder.ToTable("price_agreements", table => table.HasCheckConstraint("ck_agreement_latest",
            "\"LatestVersionId\" IS NULL AND \"LatestOrdinal\" = 0 OR \"LatestVersionId\" IS NOT NULL AND \"LatestOrdinal\" > 0"));
        builder.HasKey(x => x.Id).HasName("pk_price_agreements");
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new AgreementId(x)).ValueGeneratedNever();
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        builder.Property(x => x.CreatedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.Property(x => x.LatestVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        builder.HasAlternateKey(x => new { x.Id, x.TradingRelationshipId });
        builder.HasOne<PriceAgreementVersion>().WithMany()
            .HasForeignKey(x => new { x.Id, x.TradingRelationshipId, x.LatestVersionId, x.LatestOrdinal })
            .HasPrincipalKey(x => new { x.AgreementId, x.TradingRelationshipId, x.Id, x.Ordinal }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TradingRelationshipId, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_price_agreements_relationship_created_id");
    }
}
