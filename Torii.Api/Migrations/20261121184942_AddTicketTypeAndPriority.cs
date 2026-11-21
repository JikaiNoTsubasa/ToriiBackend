using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTypeAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Tickets",
                type: "text",
                nullable: false,
                // Existing tickets: MEDIUM (UPPERCASE value, see UpperCaseEnumConverter).
                defaultValue: "MEDIUM");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Tickets",
                type: "text",
                nullable: false,
                // Existing tickets: REQUEST.
                defaultValue: "REQUEST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Tickets");
        }
    }
}
