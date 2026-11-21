using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// User administration. Deletion is a soft delete. Safeguards: an admin cannot delete,
/// disable or demote their own account, and the last active admin cannot lose that
/// status. Disabling or deleting a user revokes their refresh tokens (sessions end when
/// the current access token expires).
/// </summary>
public class UserManager(ToriiDbContext db) : ToriManager(db)
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int MaxEmailLength = 256; // Users.Email column (ToriiDbContext)

    private IQueryable<User> NotDeleted => _dbContext.Users.Where(u => !u.IsDeleted);

    /// <summary>
    /// Lists users by email or display name (case-insensitive), alphabetically.
    /// <paramref name="activeOnly"/> hides disabled users (e.g. when picking members).
    /// </summary>
    public async Task<IReadOnlyList<AdminUserDto>> ListAsync(
        string? search, bool activeOnly, int? take, CancellationToken ct = default)
    {
        var query = NotDeleted.AsNoTracking();

        if (activeOnly)
            query = query.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern, "\\")
                                  || EF.Functions.ILike(u.DisplayName, pattern, "\\"));
        }

        return await query
            .OrderBy(u => u.DisplayName)
            .Take(Math.Clamp(take ?? DefaultTake, 1, MaxTake))
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.DisplayName, u.Position, u.AvatarUrl, u.Role, u.IsActive,
                u.Customers.Count(c => !c.IsDeleted), u.CreatedAt, u.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<AdminUserDto> GetAsync(long id, CancellationToken ct = default)
        => await NotDeleted
               .AsNoTracking()
               .Where(u => u.Id == id)
               .Select(u => new AdminUserDto(
                   u.Id, u.Email, u.DisplayName, u.Position, u.AvatarUrl, u.Role, u.IsActive,
                   u.Customers.Count(c => !c.IsDeleted), u.CreatedAt, u.UpdatedAt))
               .FirstOrDefaultAsync(ct)
           ?? throw NotFoundException.For("User", id);

    public async Task<AdminUserDto> CreateAsync(AdminUserCreateRequest request, long actorId, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        await EnsureEmailAvailableAsync(email, excludedUserId: null, ct);

        var user = new User
        {
            Email = email,
            DisplayName = NormalizeName(request.DisplayName),
            Position = User.NormalizePosition(request.Position),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            CreatedById = actorId,
            UpdatedAt = null,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        return await GetAsync(user.Id, ct);
    }

    public async Task<AdminUserDto> UpdateAsync(long id, AdminUserUpdateRequest request, long actorId, CancellationToken ct = default)
    {
        var user = await FindTrackedAsync(id, ct);

        var losesAdmin = user is { Role: UserRole.ADMIN, IsActive: true }
                         && (request.Role != UserRole.ADMIN || !request.IsActive);
        if (losesAdmin)
            await EnsureCanLoseAdminAsync(user, actorId, ct);

        var email = NormalizeEmail(request.Email);
        if (email != user.Email)
            await EnsureEmailAvailableAsync(email, excludedUserId: id, ct);

        user.Email = email;
        user.DisplayName = NormalizeName(request.DisplayName);
        user.Position = User.NormalizePosition(request.Position);
        user.Role = request.Role;
        if (!string.IsNullOrEmpty(request.NewPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        var disabling = user.IsActive && !request.IsActive;
        user.IsActive = request.IsActive;
        if (disabling)
            await RevokeSessionsAsync(user.Id, ct);

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = actorId;
        await _dbContext.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, long actorId, CancellationToken ct = default)
    {
        var user = await FindTrackedAsync(id, ct);
        if (user is { Role: UserRole.ADMIN, IsActive: true })
            await EnsureCanLoseAdminAsync(user, actorId, ct);
        else if (user.Id == actorId)
            throw new ConflictException("You cannot delete your own account.");

        var now = DateTime.UtcNow;
        user.Email = DeletedEmail(user.Id, now, user.Email);
        user.IsDeleted = true;
        user.IsActive = false;
        user.DeletedAt = now;
        user.DeletedById = actorId;
        await RevokeSessionsAsync(user.Id, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Email given to a deleted account, freeing the original address for a new account:
    /// <c>deleted-{id}-{unix seconds}-{original email}</c> (the original is kept for audit),
    /// truncated to the column length. The id prefix keeps it unique, and nobody can sign
    /// in with it. Same format as the <c>ReleaseDeletedUserEmails</c> migration.
    /// </summary>
    private static string DeletedEmail(long id, DateTime deletedAt, string email)
    {
        var released = $"deleted-{id}-{new DateTimeOffset(deletedAt).ToUnixTimeSeconds()}-{email}";
        return released.Length <= MaxEmailLength ? released : released[..MaxEmailLength];
    }

    /// <summary>The actor keeps their own admin rights, and at least one active admin remains.</summary>
    private async Task EnsureCanLoseAdminAsync(User user, long actorId, CancellationToken ct)
    {
        if (user.Id == actorId)
            throw new ConflictException("You cannot delete, disable or demote your own administrator account.");

        var otherAdmins = await NotDeleted.AnyAsync(u =>
            u.Id != user.Id && u.IsActive && u.Role == UserRole.ADMIN, ct);
        if (!otherAdmins)
            throw new ConflictException("At least one active administrator must remain.");
    }

    private async Task EnsureEmailAvailableAsync(string email, long? excludedUserId, CancellationToken ct)
    {
        // Deleted accounts have a released "deleted-…" email (see DeletedEmail), so they never block.
        if (await _dbContext.Users.AnyAsync(u => u.Email == email && u.Id != excludedUserId, ct))
            throw new ConflictException("An account already exists with this email.");
    }

    private async Task RevokeSessionsAsync(long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<User> FindTrackedAsync(long id, CancellationToken ct)
        => await NotDeleted.FirstOrDefaultAsync(u => u.Id == id, ct)
           ?? throw NotFoundException.For("User", id);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length > 0
            ? trimmed
            : throw new ValidationException(
                new Dictionary<string, string[]> { ["displayName"] = ["Display name is required."] });
    }

    /// <summary>Escapes LIKE wildcards typed by the user.</summary>
    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
