using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.OrganizationAccess.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialOrganizationAccessPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organization_access");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "organization_access",
                columns: table => new
                {
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    response_status = table.Column<short>(type: "smallint", nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.person_id, x.operation, x.idempotency_key });
                    table.CheckConstraint("ck_idempotency_records_request_hash_length", "octet_length(request_hash) = 32");
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "organization_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registration_country = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    national_organization_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                    table.CheckConstraint("ck_organizations_status", "status in ('active', 'archived')");
                    table.CheckConstraint("ck_organizations_status_archived_at", "(status = 'active' and archived_at is null) or (status = 'archived' and archived_at is not null)");
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                schema: "organization_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_memberships", x => x.id);
                    table.CheckConstraint("ck_organization_memberships_status", "status in ('active', 'ended')");
                    table.CheckConstraint("ck_organization_memberships_status_ended_at", "(status = 'active' and ended_at is null) or (status = 'ended' and ended_at is not null)");
                    table.ForeignKey(
                        name: "fk_organization_memberships_organizations",
                        column: x => x.organization_id,
                        principalSchema: "organization_access",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_membership_roles",
                schema: "organization_access",
                columns: table => new
                {
                    role = table.Column<string>(type: "text", nullable: false),
                    organization_membership_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_membership_roles", x => new { x.organization_membership_id, x.role });
                    table.CheckConstraint("ck_organization_membership_roles_role", "role in ('owner', 'member')");
                    table.ForeignKey(
                        name: "fk_organization_membership_roles_memberships",
                        column: x => x.organization_membership_id,
                        principalSchema: "organization_access",
                        principalTable: "organization_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organization_memberships_active_person",
                schema: "organization_access",
                table: "organization_memberships",
                column: "person_id",
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ux_organization_memberships_active_organization_person",
                schema: "organization_access",
                table: "organization_memberships",
                columns: new[] { "organization_id", "person_id" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ux_organizations_legal_identity",
                schema: "organization_access",
                table: "organizations",
                columns: new[] { "registration_country", "national_organization_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "organization_access");

            migrationBuilder.DropTable(
                name: "organization_membership_roles",
                schema: "organization_access");

            migrationBuilder.DropTable(
                name: "organization_memberships",
                schema: "organization_access");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "organization_access");
        }
    }
}
