using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelFilesAndActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StorageKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketChannelId = table.Column<long>(type: "bigint", nullable: false),
                    StoredFileId = table.Column<long>(type: "bigint", nullable: false),
                    ForwardedFromChannelId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ChannelFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelFiles_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelFiles_TicketChannels_TicketChannelId",
                        column: x => x.TicketChannelId,
                        principalTable: "TicketChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: true),
                    ToStatus = table.Column<string>(type: "text", nullable: true),
                    ChannelFileId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketActivities_ChannelFiles_ChannelFileId",
                        column: x => x.ChannelFileId,
                        principalTable: "ChannelFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketActivities_TicketChannels_TicketChannelId",
                        column: x => x.TicketChannelId,
                        principalTable: "TicketChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketActivities_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelFiles_StoredFileId",
                table: "ChannelFiles",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelFiles_TicketChannelId_StoredFileId",
                table: "ChannelFiles",
                columns: new[] { "TicketChannelId", "StoredFileId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_StorageKey",
                table: "StoredFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ActorId",
                table: "TicketActivities",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ChannelFileId",
                table: "TicketActivities",
                column: "ChannelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketChannelId_CreatedAt",
                table: "TicketActivities",
                columns: new[] { "TicketChannelId", "CreatedAt" });

            // Backfill the feed of existing channels (only when the actor still exists):
            // their opening, and their last known status change (previous status unknown).
            migrationBuilder.Sql("""
                INSERT INTO "TicketActivities" ("TicketChannelId", "Type", "ActorId", "CreatedAt")
                SELECT ch."Id", 'CHANNEL_OPENED', ch."CreatedById", ch."CreatedAt"
                FROM "TicketChannels" ch
                WHERE EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = ch."CreatedById");

                INSERT INTO "TicketActivities" ("TicketChannelId", "Type", "ActorId", "CreatedAt", "ToStatus")
                SELECT ch."Id", 'STATUS_CHANGED', ch."StatusChangedById", ch."StatusChangedAt", ch."Status"
                FROM "TicketChannels" ch
                WHERE ch."StatusChangedAt" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = ch."StatusChangedById");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketActivities");

            migrationBuilder.DropTable(
                name: "ChannelFiles");

            migrationBuilder.DropTable(
                name: "StoredFiles");
        }
    }
}
