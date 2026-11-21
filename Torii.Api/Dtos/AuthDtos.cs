using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(128)] string DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

/// <summary>Returned after login/register/refresh.</summary>
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
