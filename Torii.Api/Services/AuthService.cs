using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Services;

public class AuthService(ToriiDbContext db, ITokenService tokens) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Un compte existe déjà avec cet e-mail.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName.Trim(),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationException("E-mail ou mot de passe invalide.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

        if (stored is null || !stored.IsActive || stored.User is null || !stored.User.IsActive)
            throw new AuthenticationException("Refresh token invalide ou expiré.");

        // Rotation : le token courant est révoqué et un nouveau est émis.
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
            : new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl);
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
            new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }
}
