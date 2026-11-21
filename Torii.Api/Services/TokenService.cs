using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Torii.Api.Models;

namespace Torii.Api.Services;

/// <summary>Options liées aux JWT, liées à la section "Jwt" de la configuration.</summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "Torii";
    public string Audience { get; set; } = "ToriiClient";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public class TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public (string token, DateTime expiresAt) CreateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    public string CreateRefreshTokenValue()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public DateTime GetRefreshTokenExpiry()
        => DateTime.UtcNow.AddDays(_opt.RefreshTokenDays);
}
