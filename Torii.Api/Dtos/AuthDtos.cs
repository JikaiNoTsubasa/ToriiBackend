using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>Account request; the account waits for an administrator's approval.</summary>
public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(128)] string DisplayName,
    [MaxLength(128)] string? Position = null);

/// <summary>Returned after /register: no session, the account is PENDING until approved.</summary>
public record RegistrationResponse(string Email, AccountStatus Status);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

/// <summary>Returned after login/refresh.</summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

/// <summary>Public representation of a user (no hash).</summary>
public record UserDto(long Id, string Email, string DisplayName, string? Position, string AvatarUrl, UserRole Role)
{
    public static UserDto From(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Position, user.AvatarUrl, user.Role);
}
