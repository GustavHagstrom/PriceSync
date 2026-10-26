using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialAgreementsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agreements");

            migrationBuilder.CreateTable(
                name: "price_agreements",
                schema: "agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_agreements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_agreements_relationship_created_id",
                schema: "agreements",
                table: "price_agreements",
                columns: new[] { "TradingRelationshipId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_agreements",
                schema: "agreements");
        }
    }
}
