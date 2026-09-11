using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;

namespace PriceSync.Modules.OrganizationAccess.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", OrganizationAccessDbContext.SchemaName);

        builder.HasKey(organization => organization.Id)
            .HasName("pk_organizations");

        builder.Property(organization => organization.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new OrganizationId(value))
            .ValueGeneratedNever();

        builder.Property(organization => organization.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.OwnsOne(
            organization => organization.LegalIdentity,
            legalIdentity =>
            {
                legalIdentity.Property(identity => identity.RegistrationCountry)
                    .HasColumnName("registration_country")
                    .HasColumnType("char(2)")
                    .HasMaxLength(2)
                    .IsRequired();

                legalIdentity.Property(identity => identity.NationalOrganizationNumber)
                    .HasColumnName("national_organization_number")
                    .HasMaxLength(32)
                    .IsRequired();

                legalIdentity.HasIndex(identity => new { identity.RegistrationCountry, identity.NationalOrganizationNumber })
                    .IsUnique()
                    .HasDatabaseName("ux_organizations_legal_identity");
            });

        builder.Navigation(organization => organization.LegalIdentity).IsRequired();

        builder.Property(organization => organization.Status)
            .HasColumnName("status")
            .HasConversion(status => status.ToString().ToLowerInvariant(), value => Enum.Parse<OrganizationStatus>(value, ignoreCase: true))
            .HasColumnType("text")
            .IsRequired();

        builder.Property(organization => organization.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(organization => organization.ArchivedAt)
            .HasColumnName("archived_at")
            .HasColumnType("timestamp with time zone");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_organizations_status",
                "status in ('active', 'archived')");

            table.HasCheckConstraint(
                "ck_organizations_status_archived_at",
                "(status = 'active' and archived_at is null) or (status = 'archived' and archived_at is not null)");
        });
    }
}
