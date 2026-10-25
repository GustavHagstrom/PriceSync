using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceSync.Modules.PricePublication.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialPricePublicationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "price_publication");

            migrationBuilder.CreateTable(
                name: "price_lists",
                schema: "price_publication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Market = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerOrganizationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.Id);
                });
            migrationBuilder.Sql("""
                CREATE FUNCTION price_publication.prevent_price_list_scope_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF ROW(NEW."Id", NEW."OwnerOrganizationId", NEW."Type", NEW."Currency", NEW."Market", NEW."CustomerOrganizationId")
                        IS DISTINCT FROM
                        ROW(OLD."Id", OLD."OwnerOrganizationId", OLD."Type", OLD."Currency", OLD."Market", OLD."CustomerOrganizationId") THEN
                        RAISE EXCEPTION 'Logical Price List scope is immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'ck_price_list_immutable_scope';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER price_list_immutable_scope BEFORE UPDATE ON price_publication.price_lists
                    FOR EACH ROW EXECUTE FUNCTION price_publication.prevent_price_list_scope_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION price_publication.prevent_price_list_scope_change() CASCADE;");
            migrationBuilder.DropTable(
                name: "price_lists",
                schema: "price_publication");
        }
    }
}
