using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing accounts were usable: they start ACTIVE. The model sets the value on every
            // insert, so this default only backfills the current rows.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AlterColumn<string>(
                name: "TicketNumber",
                table: "Notifications",
                type: "character varying(16)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)");

            migrationBuilder.AlterColumn<long>(
                name: "TicketChannelId",
                table: "Notifications",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "LastMessageId",
                table: "Notifications",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Account request notifications have no ticket: they cannot survive the NOT NULL columns.
            migrationBuilder.Sql("""DELETE FROM "Notifications" WHERE "Type" = 'ACCOUNT_REQUESTED';""");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "TicketNumber",
                table: "Notifications",
                type: "character varying(16)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "TicketChannelId",
                table: "Notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "LastMessageId",
                table: "Notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
