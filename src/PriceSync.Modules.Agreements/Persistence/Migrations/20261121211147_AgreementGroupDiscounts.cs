using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class AgreementGroupDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agreement_version_group_discounts",
                schema: "agreements",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    Discount = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_version_group_discounts", x => new { x.VersionId, x.Code });
                    table.CheckConstraint("ck_agreement_group_discount", " \"Code\" <> '' AND \"Discount\" BETWEEN 0 AND 100 AND \"Discount\" = trunc(\"Discount\", 1) ");
                    table.ForeignKey(
                        name: "FK_agreement_version_group_discounts_agreement_versions_Versio~",
                        column: x => x.VersionId,
                        principalSchema: "agreements",
                        principalTable: "agreement_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_agreement_version_validity",
                schema: "agreements",
                table: "agreement_versions",
                sql: " \"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\" ");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agreement_coverage_discount",
                schema: "agreements",
                table: "agreement_version_coverage",
                sql: "(\"Type\" = 'general' AND \"GeneralDiscount\" IS NOT NULL AND\r\n    \"GeneralDiscount\" BETWEEN 0 AND 100 AND \"GeneralDiscount\" = trunc(\"GeneralDiscount\", 1))\r\nOR (\"Type\" = 'customerSpecificNet' AND \"GeneralDiscount\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_version_group_discounts",
                schema: "agreements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agreement_version_validity",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agreement_coverage_discount",
                schema: "agreements",
                table: "agreement_version_coverage");
        }
    }
}
