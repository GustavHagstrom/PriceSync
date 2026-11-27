using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class FirstAgreementPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LatestOrdinal",
                schema: "agreements",
                table: "price_agreements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestVersionId",
                schema: "agreements",
                table: "price_agreements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_price_agreements_Id_TradingRelationshipId",
                schema: "agreements",
                table: "price_agreements",
                columns: new[] { "Id", "TradingRelationshipId" });

            migrationBuilder.CreateTable(
                name: "agreement_versions",
                schema: "agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    Midpoint = table.Column<string>(type: "text", nullable: false),
                    RoundingStage = table.Column<string>(type: "text", nullable: false),
                    PreservePriceQuantityAndUnitOfMeasure = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_versions", x => x.Id);
                    table.UniqueConstraint("AK_agreement_versions_AgreementId_TradingRelationshipId_Id_Ord~", x => new { x.AgreementId, x.TradingRelationshipId, x.Id, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_agreement_versions_price_agreements_AgreementId_TradingRela~",
                        columns: x => new { x.AgreementId, x.TradingRelationshipId },
                        principalSchema: "agreements",
                        principalTable: "price_agreements",
                        principalColumns: new[] { "Id", "TradingRelationshipId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agreement_version_coverage",
                schema: "agreements",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    GeneralDiscount = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_version_coverage", x => new { x.VersionId, x.PriceListId });
                    table.ForeignKey(
                        name: "FK_agreement_version_coverage_agreement_versions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "agreements",
                        principalTable: "agreement_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_price_agreements_Id_TradingRelationshipId_LatestVersionId_L~",
                schema: "agreements",
                table: "price_agreements",
                columns: new[] { "Id", "TradingRelationshipId", "LatestVersionId", "LatestOrdinal" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_agreement_latest",
                schema: "agreements",
                table: "price_agreements",
                sql: "\"LatestVersionId\" IS NULL AND \"LatestOrdinal\" = 0 OR \"LatestVersionId\" IS NOT NULL AND \"LatestOrdinal\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_agreement_versions_AgreementId_Ordinal",
                schema: "agreements",
                table: "agreement_versions",
                columns: new[] { "AgreementId", "Ordinal" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_price_agreements_agreement_versions_Id_TradingRelationshipI~",
                schema: "agreements",
                table: "price_agreements",
                columns: new[] { "Id", "TradingRelationshipId", "LatestVersionId", "LatestOrdinal" },
                principalSchema: "agreements",
                principalTable: "agreement_versions",
                principalColumns: new[] { "AgreementId", "TradingRelationshipId", "Id", "Ordinal" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_price_agreements_agreement_versions_Id_TradingRelationshipI~",
                schema: "agreements",
                table: "price_agreements");

            migrationBuilder.DropTable(
                name: "agreement_version_coverage",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "agreement_versions",
                schema: "agreements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_price_agreements_Id_TradingRelationshipId",
                schema: "agreements",
                table: "price_agreements");

            migrationBuilder.DropIndex(
                name: "IX_price_agreements_Id_TradingRelationshipId_LatestVersionId_L~",
                schema: "agreements",
                table: "price_agreements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agreement_latest",
                schema: "agreements",
                table: "price_agreements");

            migrationBuilder.DropColumn(
                name: "LatestOrdinal",
                schema: "agreements",
                table: "price_agreements");

            migrationBuilder.DropColumn(
                name: "LatestVersionId",
                schema: "agreements",
                table: "price_agreements");
        }
    }
}
