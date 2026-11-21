using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TicketChannels",
                type: "text",
                nullable: false,
                // Existing channels: NEW (UPPERCASE value, see UpperCaseEnumConverter).
                defaultValue: "NEW");

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAt",
                table: "TicketChannels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StatusChangedById",
                table: "TicketChannels",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TicketChannels");

            migrationBuilder.DropColumn(
                name: "StatusChangedAt",
                table: "TicketChannels");

            migrationBuilder.DropColumn(
                name: "StatusChangedById",
                table: "TicketChannels");
        }
    }
}
