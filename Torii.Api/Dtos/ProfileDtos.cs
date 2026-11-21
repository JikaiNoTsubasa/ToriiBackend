using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>The signed-in user's own profile.</summary>
public record ProfileDto(
    long Id,
    string Email,
    string DisplayName,
    string? Position,
    string AvatarUrl,
    bool HasCustomAvatar,
    UserRole Role,
    DateTime CreatedAt,
    IReadOnlyList<CustomerRefDto> Customers);

public record UpdateProfileRequest(
    [Required, MaxLength(128)] string DisplayName,
    [MaxLength(128)] string? Position = null);

/// <summary>Email change, confirmed with the current password.</summary>
public record ChangeEmailRequest(
    [Required, EmailAddress, MaxLength(256)] string NewEmail,
    [Required] string CurrentPassword);

/// <summary>Password change; every session is revoked and a fresh one is returned.</summary>
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
