namespace Torii.Api.Models;

/// <summary>
/// Refresh token persistant permettant de renouveler un access token JWT
/// sans re-login. Rotation à chaque usage (le token utilisé est révoqué).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Valeur opaque (aléatoire cryptographique), unique.</summary>
    public required string Token { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    /// <summary>Token utilisable : non révoqué et non expiré.</summary>
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
