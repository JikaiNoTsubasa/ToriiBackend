using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Services;

public class AuthService(
    ToriiDbContext db,
    ITokenService tokens,
    NotificationManager notifications,
    ILogger<AuthService> logger) : IAuthService
{
    public Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => RequestAccountAsync(
            new AccountRequest(request.Email, request.DisplayName, request.Position,
                BCrypt.Net.BCrypt.HashPassword(request.Password), AccountType.APPLICATION),
            ct);

    public async Task<RegistrationResponse> RequestAccountAsync(AccountRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Display name is required."],
            });

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account already exists with this email.");
        if (request.SingpassSubject is not null &&
            await db.Users.AnyAsync(u => u.SingpassSubject == request.SingpassSubject, ct))
            throw new ConflictException("An account is already linked to this Singpass account.");

        var user = new User
        {
            Email = email,
            PasswordHash = request.PasswordHash,
            DisplayName = displayName,
            Position = User.NormalizePosition(request.Position),
            AccountType = request.AccountType,
            SingpassSubject = request.SingpassSubject,
            Status = AccountStatus.PENDING,
            IsActive = false,
            UpdatedAt = null,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await notifications.NotifyAccountRequestAsync(user.Id, ct);

        logger.LogInformation("Account requested ({AccountType}): {Email} (user {UserId}).",
            user.AccountType, email, user.Id);
        return new RegistrationResponse(user.Email, user.Status);
    }

    public Task<AuthResponse> SignInAsync(User user, CancellationToken ct = default)
    {
        if (user.Status != AccountStatus.ACTIVE)
            throw new AccountNotActiveException(user.Status);
        if (!user.IsActive || user.IsDeleted)
            throw new AuthenticationException("This account is disabled.");
        return IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationException("Invalid email or password.");

        // Only revealed once the password is verified: no way to probe who has requested an account.
        if (user.Status != AccountStatus.ACTIVE)
            throw new AccountNotActiveException(user.Status);
        if (!user.IsActive)
            throw new AuthenticationException("Invalid email or password.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

        if (stored is null || !stored.IsActive || stored.User is null || !stored.User.IsActive)
            throw new AuthenticationException("Invalid or expired refresh token.");

        // Rotation: the current token is revoked and a new one is issued.
        stored.RevokedAt = DateTime.UtcNow;
        var response = await IssueTokensAsync(stored.User, ct);
        return response;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        return user is null
            ? null
            : UserDto.From(user);
    }

    public async Task<AuthResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, ct)
                   ?? throw NotFoundException.For("User", userId);

        // 400, not 401: the client would take a 401 for an expired session.
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["The current password is incorrect."],
            });
        if (request.CurrentPassword == request.NewPassword)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["The new password must differ from the current one."],
            });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = user.Id;
        await RevokeAllSessionsAsync(userId, ct);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task RevokeAllSessionsAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (accessToken, accessExpiry) = tokens.CreateAccessToken(user);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = tokens.CreateRefreshTokenValue(),
            ExpiresAt = tokens.GetRefreshTokenExpiry(),
        };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            refresh.Token,
            accessExpiry,
            UserDto.From(user));
    }
}
