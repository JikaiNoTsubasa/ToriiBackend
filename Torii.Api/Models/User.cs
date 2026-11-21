using System.ComponentModel.DataAnnotations;

namespace Torii.Api.Models;

/// <summary>
/// Torii application user.
/// </summary>
public class User: Entity
{

    /// <summary>Email address, used as the sign-in identifier (unique).</summary>
    public required string Email { get; set; }

    /// <summary>BCrypt password hash. Never exposed by the API.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>Name displayed in the UI.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Application role; accounts created through /register are USER.</summary>
    public UserRole Role { get; set; } = UserRole.USER;

    /// <summary>Refresh tokens issued to this user.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Customers the user is a member of (N-N through CustomerMembers).</summary>
    public ICollection<Customer> Customers { get; set; } = [];

    public string AvatarUrl { get; set; } = "https://www.gravatar.com/avatar/?d=mp&s=64";

    public AccountType AccountType = AccountType.APPLICATION;
}
