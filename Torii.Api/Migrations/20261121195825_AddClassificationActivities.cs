using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromPriority",
                table: "TicketActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromType",
                table: "TicketActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToPriority",
                table: "TicketActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToType",
                table: "TicketActivities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromPriority",
                table: "TicketActivities");

            migrationBuilder.DropColumn(
                name: "FromType",
                table: "TicketActivities");

            migrationBuilder.DropColumn(
                name: "ToPriority",
                table: "TicketActivities");

            migrationBuilder.DropColumn(
                name: "ToType",
                table: "TicketActivities");
        }
    }
}
