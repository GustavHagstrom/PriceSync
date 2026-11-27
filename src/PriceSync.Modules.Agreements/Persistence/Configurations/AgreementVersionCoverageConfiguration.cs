using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class AgreementVersionCoverageConfiguration : IEntityTypeConfiguration<AgreementVersionCoverage>
{
    public void Configure(EntityTypeBuilder<AgreementVersionCoverage> builder)
    {
        builder.ToTable("agreement_version_coverage", table => table.HasCheckConstraint("ck_agreement_coverage_discount",
            """
            ("Type" = 'general' AND "GeneralDiscount" IS NOT NULL AND
                "GeneralDiscount" BETWEEN 0 AND 100 AND "GeneralDiscount" = trunc("GeneralDiscount", 1))
            OR ("Type" = 'customerSpecificNet' AND "GeneralDiscount" IS NULL)
            """));
        builder.HasKey(x => new { x.VersionId, x.PriceListId });
        builder.Property(x => x.VersionId).HasConversion(x => x.Value, x => new AgreementVersionId(x));
        builder.Property(x => x.PriceListId).HasConversion(x => x.Value, x => new PriceListId(x));
        builder.Property(x => x.GeneralDiscount).HasColumnType("numeric");
        builder.HasOne<PriceAgreementVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
