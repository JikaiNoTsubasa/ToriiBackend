using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>
/// Pending account request, as reviewed by an administrator; <see cref="AccountType"/> tells how
/// the person will sign in (email + password, or Singpass).
/// </summary>
public record AccountRequestDto(
    long Id,
    string Email,
    string DisplayName,
    string? Position,
    string AvatarUrl,
    AccountType AccountType,
    DateTime RequestedAt);
