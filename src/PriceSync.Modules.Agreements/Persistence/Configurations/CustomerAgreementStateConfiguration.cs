using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class CustomerAgreementStateConfiguration : IEntityTypeConfiguration<CustomerAgreementState>
{
    public void Configure(EntityTypeBuilder<CustomerAgreementState> builder)
    {
        builder.ToTable("customer_agreement_states", table => table.HasCheckConstraint("ck_customer_agreement_state_sequence",
            """ "Sequence" > 0 OR ("Sequence" = 0 AND "ActiveVersionId" IS NULL) """));
        builder.HasKey(x => x.TradingRelationshipId);
        builder.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x))
            .ValueGeneratedNever();
        builder.Property(x => x.ActiveVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        builder.HasOne<AgreementAcceptance>().WithMany()
            .HasForeignKey(x => new { x.TradingRelationshipId, x.ActiveVersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
