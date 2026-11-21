using System.ComponentModel.DataAnnotations;

namespace Torii.Api.Dtos;

/// <summary>Customer as listed in the administration.</summary>
public record CustomerListItemDto(long Id, string Name, int MemberCount, DateTime CreatedAt);

/// <summary>Customer details with its members.</summary>
public record CustomerDetailDto(long Id, string Name, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<UserDto> Members);

/// <summary>Customer creation / rename.</summary>
public record CustomerUpsertRequest(
    [Required, MaxLength(128)] string Name);

/// <summary>Adds an existing user as a member of a customer.</summary>
public record AddCustomerMemberRequest(
    [Range(1, long.MaxValue)] long UserId);
