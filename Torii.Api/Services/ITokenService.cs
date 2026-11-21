using System.Security.Claims;
using Torii.Api.Models;

namespace Torii.Api.Services;

public interface ITokenService
{
    /// <summary>Génère un access token JWT signé pour l'utilisateur.</summary>
    (string token, DateTime expiresAt) CreateAccessToken(User user);

    /// <summary>Génère une valeur opaque aléatoire pour un refresh token.</summary>
    string CreateRefreshTokenValue();

    /// <summary>Durée de vie d'un refresh token.</summary>
    DateTime GetRefreshTokenExpiry();
}
