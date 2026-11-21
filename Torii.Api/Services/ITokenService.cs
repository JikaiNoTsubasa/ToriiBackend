using System.Security.Claims;
using Torii.Api.Models;

namespace Torii.Api.Services;

public interface ITokenService
{
    /// <summary>Generates a signed JWT access token for the user.</summary>
    (string token, DateTime expiresAt) CreateAccessToken(User user);

    /// <summary>Generates a random opaque value for a refresh token.</summary>
    string CreateRefreshTokenValue();

    /// <summary>Lifetime of a refresh token.</summary>
    DateTime GetRefreshTokenExpiry();
}
