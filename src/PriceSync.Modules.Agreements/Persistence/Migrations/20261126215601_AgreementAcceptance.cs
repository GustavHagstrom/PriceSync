using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class AgreementAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_agreement_versions_TradingRelationshipId_Id",
                schema: "agreements",
                table: "agreement_versions",
                columns: new[] { "TradingRelationshipId", "Id" });

            migrationBuilder.CreateTable(
                name: "agreement_acceptances",
                schema: "agreements",
                columns: table => new
                {
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedByPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_acceptances", x => new { x.TradingRelationshipId, x.VersionId });
                    table.ForeignKey(
                        name: "FK_agreement_acceptances_agreement_versions_TradingRelationshi~",
                        columns: x => new { x.TradingRelationshipId, x.VersionId },
                        principalSchema: "agreements",
                        principalTable: "agreement_versions",
                        principalColumns: new[] { "TradingRelationshipId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_acceptances",
                schema: "agreements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_agreement_versions_TradingRelationshipId_Id",
                schema: "agreements",
                table: "agreement_versions");
        }
    }
}
