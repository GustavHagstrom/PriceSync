using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class AgreementVersionPredecessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions",
                type: "uuid",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agreement_versions_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropIndex(
                name: "IX_agreement_versions_PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");

            migrationBuilder.DropColumn(
                name: "PredecessorVersionId",
                schema: "agreements",
                table: "agreement_versions");
        }
    }
}
