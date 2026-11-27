using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class PriceAgreementVersionConfiguration : IEntityTypeConfiguration<PriceAgreementVersion>
{
    public void Configure(EntityTypeBuilder<PriceAgreementVersion> builder)
    {
        builder.ToTable("agreement_versions", table => table.HasCheckConstraint("ck_agreement_version_validity",
            """ "ValidTo" IS NULL OR "ValidTo" >= "ValidFrom" """));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new AgreementVersionId(x)).ValueGeneratedNever();
        builder.Property(x => x.AgreementId).HasConversion(x => x.Value, x => new AgreementId(x));
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        builder.Property(x => x.PublishedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.Property(x => x.PredecessorVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        builder.HasOne<PriceAgreementVersion>().WithMany()
            .HasForeignKey(x => new { x.AgreementId, x.PredecessorVersionId })
            .HasPrincipalKey(x => new { x.AgreementId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AgreementId, x.Ordinal }).IsUnique();
        builder.HasAlternateKey(x => new { x.AgreementId, x.TradingRelationshipId, x.Id, x.Ordinal });
        builder.HasOne<PriceAgreement>().WithMany().HasForeignKey(x => new { x.AgreementId, x.TradingRelationshipId })
            .HasPrincipalKey(x => new { x.Id, x.TradingRelationshipId }).OnDelete(DeleteBehavior.Restrict);
    }
}
