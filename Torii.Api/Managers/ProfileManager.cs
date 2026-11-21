using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;
using Torii.Api.Services;

namespace Torii.Api.Managers;

/// <summary>
/// The signed-in user's own profile: display name, email (password confirmed) and avatar.
/// Avatars are stored with <see cref="IFileStorage"/> and served publicly by id (an
/// <c>&lt;img&gt;</c> cannot send the Bearer token); only real images are accepted
/// (checked from the file signature, not the declared type).
/// </summary>
public class ProfileManager(ToriiDbContext db, IFileStorage storage) : ToriManager(db)
{
    private const long MaxAvatarBytes = 5L * 1024 * 1024;

    /// <summary>A stored avatar opened for display.</summary>
    public sealed record AvatarContent(Stream Content, string ContentType);

    public async Task<ProfileDto> GetAsync(long userId, CancellationToken ct = default)
        => await _dbContext.Users
               .AsNoTracking()
               .Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
               .Select(u => new ProfileDto(
                   u.Id,
                   u.Email,
                   u.DisplayName,
                   u.AvatarUrl,
                   u.AvatarStorageKey != null,
                   u.Role,
                   u.CreatedAt,
                   u.Customers
                       .Where(c => !c.IsDeleted)
                       .OrderBy(c => c.Name)
                       .Select(c => new CustomerRefDto(c.Id, c.Name))
                       .ToList()))
               .FirstOrDefaultAsync(ct)
           ?? throw NotFoundException.For("User", userId);

    public async Task<ProfileDto> UpdateAsync(long userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(userId, ct);
        var name = request.DisplayName.Trim();
        if (name.Length == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["displayName"] = ["Display name is required."] });

        user.DisplayName = name;
        Touch(user);
        await _dbContext.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    /// <summary>Changes the email after checking the current password (400 when wrong, 409 when taken).</summary>
    public async Task<ProfileDto> ChangeEmailAsync(long userId, ChangeEmailRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(userId, ct);
        EnsurePassword(user, request.CurrentPassword);

        var email = request.NewEmail.Trim().ToLowerInvariant();
        if (email != user.Email)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == email && u.Id != userId, ct))
                throw new ConflictException("An account already exists with this email.");
            user.Email = email;
            Touch(user);
            await _dbContext.SaveChangesAsync(ct);
        }
        return await GetAsync(userId, ct);
    }

    /// <summary>Replaces the avatar with an uploaded image (PNG, JPEG, WebP or GIF, 5 MB max).</summary>
    public async Task<ProfileDto> UploadAvatarAsync(long userId, IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new ValidationException("The image is empty.");
        if (file.Length > MaxAvatarBytes)
            throw new ValidationException("The avatar must not exceed 5 MB.");

        await using var content = new MemoryStream();
        await file.CopyToAsync(content, ct);
        var contentType = DetectImageType(content.GetBuffer().AsSpan(0, (int)content.Length))
                          ?? throw new ValidationException("The avatar must be a PNG, JPEG, WebP or GIF image.");

        var user = await FindAsync(userId, ct);
        content.Position = 0;
        var key = await storage.SaveAsync(content, ct);
        var previousKey = user.AvatarStorageKey;

        user.AvatarStorageKey = key;
        user.AvatarContentType = contentType;
        user.AvatarUrl = $"/api/users/{user.Id}/avatar?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        Touch(user);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(key);
            throw;
        }

        if (previousKey is not null)
            storage.Delete(previousKey);
        return await GetAsync(userId, ct);
    }

    /// <summary>Back to the default avatar (the uploaded image is deleted).</summary>
    public async Task<ProfileDto> RemoveAvatarAsync(long userId, CancellationToken ct = default)
    {
        var user = await FindAsync(userId, ct);
        var previousKey = user.AvatarStorageKey;

        user.AvatarStorageKey = null;
        user.AvatarContentType = null;
        user.AvatarUrl = User.DefaultAvatarUrl;
        Touch(user);
        await _dbContext.SaveChangesAsync(ct);

        if (previousKey is not null)
            storage.Delete(previousKey);
        return await GetAsync(userId, ct);
    }

    /// <summary>Uploaded avatar of a (non-deleted) user, or null when they use the default one.</summary>
    public async Task<AvatarContent?> OpenAvatarAsync(long userId, CancellationToken ct = default)
    {
        var avatar = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted && u.AvatarStorageKey != null)
            .Select(u => new { u.AvatarStorageKey, u.AvatarContentType })
            .FirstOrDefaultAsync(ct);
        return avatar is null
            ? null
            : new AvatarContent(storage.OpenRead(avatar.AvatarStorageKey!), avatar.AvatarContentType ?? "image/png");
    }

    private async Task<User> FindAsync(long userId, CancellationToken ct)
        => await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, ct)
           ?? throw NotFoundException.For("User", userId);

    /// <summary>
    /// Wrong current password → 400 (not 401: the client would take it for an expired session).
    /// </summary>
    internal static void EnsurePassword(User user, string password)
    {
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["The current password is incorrect."],
            });
    }

    private static void Touch(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = user.Id;
    }

    /// <summary>Image type from the file signature (magic bytes), or null when not a supported image.</summary>
    private static string? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
            return "image/gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}
