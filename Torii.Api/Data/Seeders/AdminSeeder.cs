using Microsoft.EntityFrameworkCore;
using Torii.Api.Models;

namespace Torii.Api.Data.Seeders;

/// <summary>Initial admin account, bound to the "Seed:Admin" configuration section.</summary>
public class AdminSeedOptions
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "Administrator";
}

/// <summary>
/// Creates the first ADMIN account at startup, as long as no admin exists in the database.
/// Idempotent: does nothing once an admin exists. Without a <c>Seed:Admin</c>
/// configuration, it only logs a warning. The password is never written in code
/// or in a migration: provide it through user-secrets or an environment variable
/// (<c>Seed__Admin__Password</c>).
/// </summary>
public class AdminSeeder(ToriiDbContext db, IConfiguration configuration, ILogger<AdminSeeder> logger)
{
    private const int MinPasswordLength = 8; // matches RegisterRequest

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.ADMIN, ct))
            return;

        var options = configuration.GetSection("Seed:Admin").Get<AdminSeedOptions>() ?? new AdminSeedOptions();
        var email = options.Email.Trim().ToLowerInvariant();

        if (email.Length == 0 || options.Password.Length == 0)
        {
            logger.LogWarning("No admin account in the database and Seed:Admin (Email/Password) is not configured: seed skipped.");
            return;
        }

        if (options.Password.Length < MinPasswordLength)
        {
            logger.LogError("Seed:Admin:Password must be at least {Min} characters long: seed skipped.", MinPasswordLength);
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            // Never silently promote an existing account.
            logger.LogWarning("A user {Email} already exists without the ADMIN role: seed skipped.", email);
            return;
        }

        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.Password),
            DisplayName = options.DisplayName.Trim(),
            Role = UserRole.ADMIN,
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Initial admin account created: {Email}.", email);
    }
}

public static class SeederExtensions
{
    /// <summary>Runs the seeders at startup (expects an already migrated database).</summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();
    }
}
