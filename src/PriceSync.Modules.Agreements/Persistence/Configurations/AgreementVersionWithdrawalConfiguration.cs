using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class AgreementVersionWithdrawalConfiguration : IEntityTypeConfiguration<AgreementVersionWithdrawal>
{
    public void Configure(EntityTypeBuilder<AgreementVersionWithdrawal> builder)
    {
        builder.ToTable("agreement_version_withdrawals");
        builder.HasKey(x => x.VersionId).HasName(AgreementsDbContext.WithdrawalKey);
        builder.Property(x => x.VersionId).HasConversion(x => x.Value, x => new AgreementVersionId(x)).ValueGeneratedNever();
        builder.Property(x => x.WithdrawnByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.HasOne<PriceAgreementVersion>().WithOne().HasForeignKey<AgreementVersionWithdrawal>(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
