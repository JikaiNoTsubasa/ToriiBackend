using System.ComponentModel.DataAnnotations;

namespace Torii.Api.Models;

/// <summary>
/// Utilisateur de l'application Torii. Modèle minimal nécessaire au login.
/// Le modèle de données métier (projets, tâches, etc.) sera ajouté à côté.
/// </summary>
public class User: Entity
{

    /// <summary>Adresse e-mail, sert d'identifiant de connexion (unique).</summary>
    public required string Email { get; set; }

    /// <summary>Hash BCrypt du mot de passe. Jamais exposé côté API.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>Nom affiché dans l'interface.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Refresh tokens émis pour cet utilisateur.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string AvatarUrl { get; set; } = "https://www.gravatar.com/avatar/?d=mp&s=64";
}
