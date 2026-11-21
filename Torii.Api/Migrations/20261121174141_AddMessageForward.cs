using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageForward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ForwardedFromId",
                table: "TicketMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_ForwardedFromId",
                table: "TicketMessages",
                column: "ForwardedFromId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketMessages_TicketMessages_ForwardedFromId",
                table: "TicketMessages",
                column: "ForwardedFromId",
                principalTable: "TicketMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketMessages_TicketMessages_ForwardedFromId",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_ForwardedFromId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "ForwardedFromId",
                table: "TicketMessages");
        }
    }
}
