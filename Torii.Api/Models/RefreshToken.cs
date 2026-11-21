namespace Torii.Api.Models;

/// <summary>
/// Persistent refresh token used to renew a JWT access token
/// without signing in again. Rotated on every use (the used token is revoked).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Opaque value (cryptographically random), unique.</summary>
    public required string Token { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    /// <summary>Usable token: not revoked and not expired.</summary>
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
