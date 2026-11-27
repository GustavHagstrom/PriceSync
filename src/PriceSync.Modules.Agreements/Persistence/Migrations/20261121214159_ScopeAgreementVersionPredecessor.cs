using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class ScopeAgreementVersionPredecessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agreement_versions_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropIndex(
                name: "IX_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_agreement_versions_AgreementId_Id",
                schema: "agreements",
                table: "agreement_versions",
                columns: new[] { "AgreementId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_agreement_versions_AgreementId_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions",
                columns: new[] { "AgreementId", "PredecessorVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_agreement_versions_agreement_versions_AgreementId_Predecess~",
                schema: "agreements",
                table: "agreement_versions",
                columns: new[] { "AgreementId", "PredecessorVersionId" },
                principalSchema: "agreements",
                principalTable: "agreement_versions",
                principalColumns: new[] { "AgreementId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agreement_versions_agreement_versions_AgreementId_Predecess~",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_agreement_versions_AgreementId_Id",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropIndex(
                name: "IX_agreement_versions_AgreementId_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.CreateIndex(
                name: "IX_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions",
                column: "PredecessorVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_agreement_versions_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions",
                column: "PredecessorVersionId",
                principalSchema: "agreements",
                principalTable: "agreement_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
