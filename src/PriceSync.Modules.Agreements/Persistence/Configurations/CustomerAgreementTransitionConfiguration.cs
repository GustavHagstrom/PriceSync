using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class CustomerAgreementTransitionConfiguration : IEntityTypeConfiguration<CustomerAgreementTransition>
{
    public void Configure(EntityTypeBuilder<CustomerAgreementTransition> builder)
    {
        builder.ToTable("customer_agreement_transitions", table =>
            table.HasCheckConstraint("ck_customer_agreement_transition",
                """ "Sequence" > 0 AND "FromVersionId" IS DISTINCT FROM "ToVersionId" """));
        builder.HasKey(x => new { x.TradingRelationshipId, x.Sequence });
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        builder.Property(x => x.FromVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        builder.Property(x => x.ToVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        builder.Property(x => x.PersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.HasOne<CustomerAgreementState>().WithMany()
            .HasForeignKey(x => x.TradingRelationshipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgreementAcceptance>().WithMany()
            .HasForeignKey(x => new { x.TradingRelationshipId, x.FromVersionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgreementAcceptance>().WithMany()
            .HasForeignKey(x => new { x.TradingRelationshipId, x.ToVersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
