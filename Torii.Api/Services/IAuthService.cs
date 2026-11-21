using Torii.Api.Dtos;

namespace Torii.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Gets the current user (fresh data) by id.</summary>
    Task<UserDto?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Changes the password after checking the current one (400 when wrong), revokes every
    /// session of the user and returns a fresh one for the caller.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Revokes every refresh token of the user (sign out of all devices).</summary>
    Task RevokeAllSessionsAsync(long userId, CancellationToken ct = default);
}
