using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torii.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseDeletedUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: frees the email of accounts deleted before UserManager.DeletedEmail
            // existed, with the same "deleted-{id}-{unix seconds}-{original email}" format.
            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "Email" = left(
                    'deleted-' || "Id" || '-'
                    || floor(extract(epoch FROM coalesce("DeletedAt", now())))::bigint
                    || '-' || "Email",
                    256)
                WHERE "IsDeleted" AND "Email" NOT LIKE 'deleted-%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reverted: the original address may be truncated or already reused by a
            // new account (it stays readable after the "deleted-{id}-{ts}-" prefix).
        }
    }
}
