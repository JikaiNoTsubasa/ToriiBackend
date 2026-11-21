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
}
