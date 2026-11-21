using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "TicketNumbers");

            // "TI" + at least 6 digits (TI000001…TI999999, then TI1000000 without
            // truncation). nextval() is atomic: no duplicates with concurrent inserts.
            migrationBuilder.Sql("""
                CREATE FUNCTION next_ticket_number() RETURNS text
                LANGUAGE sql VOLATILE AS $$
                    SELECT 'TI' || lpad(n::text, greatest(6, length(n::text)), '0')
                    FROM (SELECT nextval('"TicketNumbers"') AS n) AS s
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValueSql: "next_ticket_number()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Number);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.Sql("DROP FUNCTION next_ticket_number();");

            migrationBuilder.DropSequence(
                name: "TicketNumbers");
        }
    }
}
