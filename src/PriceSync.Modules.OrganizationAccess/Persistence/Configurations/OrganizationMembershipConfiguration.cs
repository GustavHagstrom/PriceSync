using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;

namespace PriceSync.Modules.OrganizationAccess.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", OrganizationAccessDbContext.SchemaName);

        builder.HasKey(membership => membership.Id)
            .HasName("pk_organization_memberships");

        builder.Property(membership => membership.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new OrganizationMembershipId(value))
            .ValueGeneratedNever();

        builder.Property(membership => membership.OrganizationId)
            .HasColumnName("organization_id")
            .HasConversion(id => id.Value, value => new OrganizationId(value))
            .IsRequired();

        builder.Property(membership => membership.PersonId)
            .HasColumnName("person_id")
            .HasConversion(id => id.Value, value => new PersonId(value))
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasConversion(status => status.ToString().ToLowerInvariant(), value => Enum.Parse<OrganizationMembershipStatus>(value, ignoreCase: true))
            .HasColumnType("text")
            .IsRequired();

        builder.Property(membership => membership.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(membership => membership.EndedAt)
            .HasColumnName("ended_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .HasConstraintName("fk_organization_memberships_organizations")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.OrganizationId, membership.PersonId })
            .IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("ux_organization_memberships_active_organization_person");

        builder.HasIndex(membership => membership.PersonId)
            .HasFilter("status = 'active'")
            .HasDatabaseName("ix_organization_memberships_active_person");

        builder.OwnsMany(
            typeof(OrganizationMembershipRoleAssignment),
            "_roles",
            roles =>
            {
                roles.ToTable("organization_membership_roles", OrganizationAccessDbContext.SchemaName);

                roles.WithOwner()
                    .HasForeignKey("OrganizationMembershipId")
                    .HasConstraintName("fk_organization_membership_roles_memberships");

                roles.Property<OrganizationMembershipId>("OrganizationMembershipId")
                    .HasColumnName("organization_membership_id")
                    .HasConversion(id => id.Value, value => new OrganizationMembershipId(value));

                roles.Property<OrganizationRole>(nameof(OrganizationMembershipRoleAssignment.Role))
                    .HasColumnName("role")
                    .HasConversion(
                        role => role.ToString()!.ToLowerInvariant(),
                        value => Enum.Parse<OrganizationRole>(value, ignoreCase: true))
                    .HasColumnType("text")
                    .IsRequired();

                roles.HasKey("OrganizationMembershipId", nameof(OrganizationMembershipRoleAssignment.Role))
                    .HasName("pk_organization_membership_roles");

                roles.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "ck_organization_membership_roles_role",
                        "role in ('owner', 'member')");
                });
            });

        builder.Navigation("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_organization_memberships_status",
                "status in ('active', 'ended')");

            table.HasCheckConstraint(
                "ck_organization_memberships_status_ended_at",
                "(status = 'active' and ended_at is null) or (status = 'ended' and ended_at is not null)");
        });
    }
}
