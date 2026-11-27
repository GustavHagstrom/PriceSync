using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Persistence.Configurations;

internal sealed class AgreementVersionGroupDiscountConfiguration : IEntityTypeConfiguration<AgreementVersionGroupDiscount>
{
    public void Configure(EntityTypeBuilder<AgreementVersionGroupDiscount> builder)
    {
        builder.ToTable("agreement_version_group_discounts", table => table.HasCheckConstraint("ck_agreement_group_discount",
            """ "Code" <> '' AND "Discount" BETWEEN 0 AND 100 AND "Discount" = trunc("Discount", 1) """));
        builder.HasKey(x => new { x.VersionId, x.Code });
        builder.Property(x => x.VersionId).HasConversion(x => x.Value, x => new AgreementVersionId(x));
        builder.Property(x => x.Code).UseCollation("C");
        builder.HasOne<PriceAgreementVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
