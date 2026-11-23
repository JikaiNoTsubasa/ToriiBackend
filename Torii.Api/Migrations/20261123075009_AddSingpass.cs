using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSingpass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SingpassSubject",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SingpassConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RedirectUri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Scopes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SigningKeyJwk = table.Column<string>(type: "text", nullable: true),
                    EncryptionKeyJwk = table.Column<string>(type: "text", nullable: true),
                    KeysGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingpassConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SingpassSubject",
                table: "Users",
                column: "SingpassSubject",
                unique: true,
                filter: "\"SingpassSubject\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SingpassConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Users_SingpassSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SingpassSubject",
                table: "Users");
        }
    }
}
