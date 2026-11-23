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

    /// <summary>Free text: the person's position / job title in their company (optional).</summary>
    public string? Position { get; set; }

    /// <summary>Trims a position; blank becomes null (no position).</summary>
    public static string? NormalizePosition(string? position)
        => string.IsNullOrWhiteSpace(position) ? null : position.Trim();

    /// <summary>Application role; accounts created through /register are USER.</summary>
    public UserRole Role { get; set; } = UserRole.USER;

    /// <summary>
    /// Approval state; accounts created through /register start PENDING (and disabled) until an
    /// administrator approves them. Signing in needs ACTIVE and <see cref="Entity.IsActive"/>.
    /// </summary>
    public AccountStatus Status { get; set; } = AccountStatus.ACTIVE;

    /// <summary>Refresh tokens issued to this user.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Customers the user is a member of (N-N through CustomerMembers).</summary>
    public ICollection<Customer> Customers { get; set; } = [];

    /// <summary>Generic avatar used until the user uploads their own.</summary>
    public const string DefaultAvatarUrl = "https://www.gravatar.com/avatar/?d=mp&s=64";

    /// <summary>
    /// Avatar shown everywhere: <see cref="DefaultAvatarUrl"/>, or the uploaded avatar served by
    /// <c>/api/users/{id}/avatar?v={version}</c> (the version busts browser caches on change).
    /// </summary>
    public string AvatarUrl { get; set; } = DefaultAvatarUrl;

    /// <summary>Storage key of the uploaded avatar (null = default avatar).</summary>
    public string? AvatarStorageKey { get; set; }
    public string? AvatarContentType { get; set; }

    /// <summary>How the user signs in: email + password, or an external provider (Singpass).</summary>
    public AccountType AccountType { get; set; } = AccountType.APPLICATION;

    /// <summary>
    /// Singpass <c>sub</c> (user UUID) of a SINGPASS account (unique); cleared when the account
    /// is deleted, so the person can request a new one.
    /// </summary>
    public string? SingpassSubject { get; set; }
}
