using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: false,
                // Existing rows: overwritten below from their first message when there is one.
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedById",
                table: "Tickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedById",
                table: "Tickets",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_CreatedById",
                table: "Tickets",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: creator and creation date = author and date of the ticket's first message.
            migrationBuilder.Sql("""
                UPDATE "Tickets" t
                SET "CreatedById" = first."AuthorId", "CreatedAt" = first."CreatedAt"
                FROM (
                    SELECT DISTINCT ON (ch."TicketNumber") ch."TicketNumber", m."AuthorId", m."CreatedAt"
                    FROM "TicketChannels" ch
                    JOIN "TicketMessages" m ON m."TicketChannelId" = ch."Id"
                    ORDER BY ch."TicketNumber", m."CreatedAt", m."Id"
                ) AS first
                WHERE t."Number" = first."TicketNumber";
                """);

            // The default only served existing rows; new tickets get CreatedAt from the app.
            migrationBuilder.Sql("""ALTER TABLE "Tickets" ALTER COLUMN "CreatedAt" DROP DEFAULT;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_CreatedById",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreatedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tickets");
        }
    }
}
