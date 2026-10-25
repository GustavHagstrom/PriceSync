using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class AgreementCreationReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agreement_creation_replays",
                schema: "agreements",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActingOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "C"),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "C"),
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseStatus = table.Column<short>(type: "smallint", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agreement_creation_replays", x => new { x.PersonId, x.ActingOrganizationId, x.Operation, x.Key });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_creation_replays",
                schema: "agreements");
        }
    }
}
