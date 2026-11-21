using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.Agreements.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class CommercialAgreementTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "GeneralDiscount",
                schema: "agreements",
                table: "agreement_version_coverage",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "GeneralDiscount",
                schema: "agreements",
                table: "agreement_version_coverage",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
