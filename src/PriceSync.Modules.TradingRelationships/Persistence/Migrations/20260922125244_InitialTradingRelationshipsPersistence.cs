using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.TradingRelationships.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTradingRelationshipsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trading_relationships");

            migrationBuilder.CreateTable(
                name: "TradingRelationships",
                schema: "trading_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_relationships", x => x.Id);
                    table.CheckConstraint("ck_trading_relationships_distinct_organizations", "supplier_organization_id <> customer_organization_id");
                    table.CheckConstraint("ck_trading_relationships_status", "status in ('active', 'ended')");
                    table.CheckConstraint("ck_trading_relationships_status_ended_at", "(status = 'active' and ended_at is null)\r\nor\r\n(status = 'ended' and ended_at is not null)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_trading_relationships_customer_status_created_at_id",
                schema: "trading_relationships",
                table: "TradingRelationships",
                columns: new[] { "customer_organization_id", "status", "created_at", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_trading_relationships_supplier_status_created_at_id",
                schema: "trading_relationships",
                table: "TradingRelationships",
                columns: new[] { "supplier_organization_id", "status", "created_at", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_trading_relationships_active_supplier_customer",
                schema: "trading_relationships",
                table: "TradingRelationships",
                columns: new[] { "supplier_organization_id", "customer_organization_id" },
                unique: true,
                filter: "status = 'active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingRelationships",
                schema: "trading_relationships");
        }
    }
}
