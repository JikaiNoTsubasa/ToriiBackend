using Torii.Api.Dtos;
using Torii.Api.Models;

namespace Torii.Api.Services;

/// <summary>
/// Account to create PENDING. <see cref="PasswordHash"/> is unusable for SINGPASS accounts
/// (they sign in with Singpass only).
/// </summary>
public sealed record AccountRequest(
    string Email,
    string DisplayName,
    string? Position,
    string PasswordHash,
    AccountType AccountType,
    string? SingpassSubject = null);

public interface IAuthService
{
    /// <summary>
    /// Creates a PENDING (disabled) account and notifies the administrators; no session is issued.
    /// </summary>
    Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Creates a PENDING account of any type and notifies the administrators (409 when taken).</summary>
    Task<RegistrationResponse> RequestAccountAsync(AccountRequest request, CancellationToken ct = default);

    /// <summary>
    /// Issues a session for a user authenticated elsewhere (Singpass): 403 when pending or
    /// declined, 401 when disabled.
    /// </summary>
    Task<AuthResponse> SignInAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Signs in. Valid credentials of a pending or declined account → 403
    /// (<see cref="Exceptions.AccountNotActiveException"/>); anything else invalid → 401.
    /// </summary>
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
