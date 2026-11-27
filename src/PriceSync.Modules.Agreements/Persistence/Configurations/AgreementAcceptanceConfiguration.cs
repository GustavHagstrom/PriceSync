using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class AgreementAcceptanceConfiguration : IEntityTypeConfiguration<AgreementAcceptance>
{
    public void Configure(EntityTypeBuilder<AgreementAcceptance> builder)
    {
        builder.ToTable("agreement_acceptances");
        builder.HasKey(x => new { x.TradingRelationshipId, x.VersionId });
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        builder.Property(x => x.VersionId).HasConversion(x => x.Value, x => new AgreementVersionId(x));
        builder.Property(x => x.AcceptedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.HasOne<PriceAgreementVersion>().WithOne()
            .HasForeignKey<AgreementAcceptance>(x => new { x.TradingRelationshipId, x.VersionId })
            .HasPrincipalKey<PriceAgreementVersion>(x => new { x.TradingRelationshipId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
