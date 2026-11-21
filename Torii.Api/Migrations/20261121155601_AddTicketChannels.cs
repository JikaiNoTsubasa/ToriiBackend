using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerMembers",
                columns: table => new
                {
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMembers", x => new { x.CustomerId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CustomerMembers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 1-N → N-N: copy existing memberships before dropping the column.
            migrationBuilder.Sql("""
                INSERT INTO "CustomerMembers" ("CustomerId", "UserId")
                SELECT "CustomerId", "Id" FROM "Users" WHERE "CustomerId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CustomerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "TicketChannels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketNumber = table.Column<string>(type: "character varying(16)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketChannels_Tickets_TicketNumber",
                        column: x => x.TicketNumber,
                        principalTable: "Tickets",
                        principalColumn: "Number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelParticipants",
                columns: table => new
                {
                    TicketChannelId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelParticipants", x => new { x.TicketChannelId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_ChannelParticipants_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelParticipants_TicketChannels_TicketChannelId",
                        column: x => x.TicketChannelId,
                        principalTable: "TicketChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelParticipants_CustomerId",
                table: "ChannelParticipants",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembers_UserId",
                table: "CustomerMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChannels_TicketNumber",
                table: "TicketChannels",
                column: "TicketNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelParticipants");

            migrationBuilder.DropTable(
                name: "TicketChannels");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "Users",
                type: "bigint",
                nullable: true);

            // N-N → 1-N: only one customer per user is possible, keep the oldest one (smallest Id).
            migrationBuilder.Sql("""
                UPDATE "Users" u SET "CustomerId" = m."CustomerId"
                FROM (SELECT "UserId", MIN("CustomerId") AS "CustomerId"
                      FROM "CustomerMembers" GROUP BY "UserId") m
                WHERE u."Id" = m."UserId";
                """);

            migrationBuilder.DropTable(
                name: "CustomerMembers");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerId",
                table: "Users",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
