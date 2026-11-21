using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>User as listed in the administration (inactive users included).</summary>
public record AdminUserDto(
    long Id,
    string Email,
    string DisplayName,
    string? Position,
    string AvatarUrl,
    UserRole Role,
    bool IsActive,
    int CustomerCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>User creation by an administrator, with an initial password.</summary>
public record AdminUserCreateRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string DisplayName,
    [Required, MinLength(8)] string Password,
    UserRole Role,
    [MaxLength(128)] string? Position = null);

/// <summary>User update; <see cref="NewPassword"/> is optional (null = unchanged).</summary>
public record AdminUserUpdateRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string DisplayName,
    UserRole Role,
    bool IsActive,
    [MinLength(8)] string? NewPassword,
    [MaxLength(128)] string? Position = null);
