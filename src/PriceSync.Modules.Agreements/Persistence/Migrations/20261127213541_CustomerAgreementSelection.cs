using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class CustomerAgreementSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_agreement_states",
                schema: "agreements",
                columns: table => new
                {
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_agreement_states", x => x.TradingRelationshipId);
                    table.CheckConstraint("ck_customer_agreement_state_sequence", " \"Sequence\" > 0 OR (\"Sequence\" = 0 AND \"ActiveVersionId\" IS NULL) ");
                    table.ForeignKey(
                        name: "FK_customer_agreement_states_agreement_acceptances_TradingRela~",
                        columns: x => new { x.TradingRelationshipId, x.ActiveVersionId },
                        principalSchema: "agreements",
                        principalTable: "agreement_acceptances",
                        principalColumns: new[] { "TradingRelationshipId", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_agreement_transitions",
                schema: "agreements",
                columns: table => new
                {
                    TradingRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    FromVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_agreement_transitions", x => new { x.TradingRelationshipId, x.Sequence });
                    table.CheckConstraint("ck_customer_agreement_transition", " \"Sequence\" > 0 AND \"FromVersionId\" IS DISTINCT FROM \"ToVersionId\" ");
                    table.ForeignKey(
                        name: "FK_customer_agreement_transitions_agreement_acceptances_Tradin~",
                        columns: x => new { x.TradingRelationshipId, x.FromVersionId },
                        principalSchema: "agreements",
                        principalTable: "agreement_acceptances",
                        principalColumns: new[] { "TradingRelationshipId", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_agreement_transitions_agreement_acceptances_Tradi~1",
                        columns: x => new { x.TradingRelationshipId, x.ToVersionId },
                        principalSchema: "agreements",
                        principalTable: "agreement_acceptances",
                        principalColumns: new[] { "TradingRelationshipId", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_agreement_transitions_customer_agreement_states_Tr~",
                        column: x => x.TradingRelationshipId,
                        principalSchema: "agreements",
                        principalTable: "customer_agreement_states",
                        principalColumn: "TradingRelationshipId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_agreement_states_TradingRelationshipId_ActiveVersi~",
                schema: "agreements",
                table: "customer_agreement_states",
                columns: new[] { "TradingRelationshipId", "ActiveVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_agreement_transitions_TradingRelationshipId_FromVe~",
                schema: "agreements",
                table: "customer_agreement_transitions",
                columns: new[] { "TradingRelationshipId", "FromVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_agreement_transitions_TradingRelationshipId_ToVers~",
                schema: "agreements",
                table: "customer_agreement_transitions",
                columns: new[] { "TradingRelationshipId", "ToVersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_agreement_transitions",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "customer_agreement_states",
                schema: "agreements");
        }
    }
}
