using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>
/// User as listed in the administration (inactive and declined users included, pending
/// account requests excluded; deleted users only on request). A deleted user's
/// <see cref="Email"/> is the released <c>deleted-{id}-{unix seconds}-{original email}</c>.
/// </summary>
public record AdminUserDto(
    long Id,
    string Email,
    string DisplayName,
    string? Position,
    string AvatarUrl,
    UserRole Role,
    bool IsActive,
    AccountStatus Status,
    AccountType AccountType,
    int CustomerCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsDeleted,
    DateTime? DeletedAt);

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
