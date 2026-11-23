using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Torii.Api.Services.Singpass;

/// <summary>
/// EC P-256 keys of the relying party, stored as JWK JSON (private part included). Singpass
/// requires one signing key (<c>use: sig</c>, ES256) and one encryption key (<c>use: enc</c>,
/// ECDH-ES+A256KW) in the JWKS we publish.
/// </summary>
public static class SingpassKeys
{
    public const string SigningAlgorithm = SecurityAlgorithms.EcdsaSha256; // ES256
    public const string EncryptionAlgorithm = "ECDH-ES+A256KW";

    /// <summary>Stored form of a key (private JWK).</summary>
    public sealed record StoredJwk(
        [property: JsonPropertyName("kty")] string Kty,
        [property: JsonPropertyName("crv")] string Crv,
        [property: JsonPropertyName("x")] string X,
        [property: JsonPropertyName("y")] string Y,
        [property: JsonPropertyName("d")] string? D,
        [property: JsonPropertyName("kid")] string Kid,
        [property: JsonPropertyName("use")] string Use,
        [property: JsonPropertyName("alg")] string Alg)
    {
        /// <summary>Same key without its private part, as published in the JWKS.</summary>
        [JsonIgnore]
        public StoredJwk Public => this with { D = null };
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>New key; the kid carries its use and creation date so it is never reused.</summary>
    public static string Generate(string use, DateTime now)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var kid = $"torii-{use}-{now:yyyyMMddHHmmss}";
        var alg = use == "sig" ? SigningAlgorithm : EncryptionAlgorithm;
        var jwk = new StoredJwk("EC", "P-256",
            Base64UrlEncoder.Encode(p.Q.X), Base64UrlEncoder.Encode(p.Q.Y), Base64UrlEncoder.Encode(p.D),
            kid, use, alg);
        return JsonSerializer.Serialize(jwk, Json);
    }

    public static StoredJwk Parse(string json)
        => JsonSerializer.Deserialize<StoredJwk>(json, Json)
           ?? throw new InvalidOperationException("Invalid stored Singpass key.");

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    /// <summary>ECDsa instance of a stored key (private part included when present).</summary>
    public static ECDsa ToECDsa(StoredJwk jwk)
        => ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64UrlEncoder.DecodeBytes(jwk.X), Y = Base64UrlEncoder.DecodeBytes(jwk.Y) },
            D = jwk.D is null ? null : Base64UrlEncoder.DecodeBytes(jwk.D),
        });
}
