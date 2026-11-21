using System.ComponentModel.DataAnnotations;

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

/// <summary>Renvoyé après login/register/refresh.</summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

/// <summary>Représentation publique d'un utilisateur (sans hash).</summary>
public record UserDto(long Id, string Email, string DisplayName, string AvatarUrl);
