using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jose;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Services.Singpass;

/// <summary>Endpoints of the Singpass authorization server, from its discovery document.</summary>
public sealed record SingpassMetadata(
    string Issuer,
    string AuthorizationEndpoint,
    string PushedAuthorizationRequestEndpoint,
    string TokenEndpoint,
    string JwksUri);

/// <summary>
/// One sign-in attempt, kept server-side between the PAR and the callback: the PKCE verifier,
/// the nonce expected in the ID token, and the ephemeral DPoP key (private JWK) of the session.
/// </summary>
public sealed record SingpassAuthSession(
    string State,
    string Nonce,
    string CodeVerifier,
    string DpopKeyJwk,
    string Issuer,
    string ClientId,
    string RedirectUri);

/// <summary>Identity read from a validated ID token (name / email only with their scopes).</summary>
public sealed record SingpassIdentity(string Subject, string? Name, string? Email);

/// <summary>
/// Singpass FAPI 2.0 protocol (Login apps): Pushed Authorization Request with PKCE (S256),
/// <c>private_key_jwt</c> client assertions, DPoP proofs bound to an ephemeral key per sign-in,
/// then the token exchange and the encrypted ID token (JWE ECDH-ES+A256KW wrapping an ES256 JWS),
/// decrypted with our encryption key and verified against the Singpass JWKS.
/// </summary>
public sealed class SingpassClient(HttpClient http, IMemoryCache cache, ILogger<SingpassClient> logger)
{
    private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    /// <summary>Mandatory for Login apps: plain sign-in.</summary>
    private const string AuthenticationContextType = "APP_AUTHENTICATION_DEFAULT";
    /// <summary>Client assertions and DPoP proofs: Singpass accepts at most 2 minutes.</summary>
    private static readonly TimeSpan ProofLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MetadataCacheDuration = TimeSpan.FromHours(1);
    private static readonly string[] SignatureAlgorithms = ["ES256", "ES384", "ES512"];

    private static readonly JsonWebTokenHandler Tokens = new() { SetDefaultTimesOnTokenCreation = false };

    // --- Discovery ---

    /// <summary>Discovery document of the configured environment (cached one hour, as Singpass asks).</summary>
    public async Task<SingpassMetadata> GetMetadataAsync(SingpassConfig config, CancellationToken ct = default)
    {
        var key = $"singpass:metadata:{config.Issuer}";
        if (cache.TryGetValue(key, out SingpassMetadata? cached) && cached is not null)
            return cached;

        using var doc = await GetJsonAsync($"{config.Issuer}/.well-known/openid-configuration", ct);
        var root = doc.RootElement;
        var metadata = new SingpassMetadata(
            Required(root, "issuer"),
            Required(root, "authorization_endpoint"),
            Required(root, "pushed_authorization_request_endpoint"),
            Required(root, "token_endpoint"),
            Required(root, "jwks_uri"));

        if (metadata.Issuer != config.Issuer)
            logger.LogWarning("Singpass discovery issuer {Issuer} differs from the expected {Expected}.",
                metadata.Issuer, config.Issuer);

        cache.Set(key, metadata, MetadataCacheDuration);
        return metadata;
    }

    // --- Authorization ---

    /// <summary>
    /// Pushes the authorization request and returns the URL to send the browser to, with the
    /// session to keep until the callback.
    /// </summary>
    public async Task<(string AuthorizationUrl, SingpassAuthSession Session)> PushAuthorizationAsync(
        SingpassConfig config, CancellationToken ct = default)
    {
        var metadata = await GetMetadataAsync(config, ct);
        var session = new SingpassAuthSession(
            State: RandomToken(32),
            Nonce: RandomToken(32),
            CodeVerifier: RandomToken(48),
            DpopKeyJwk: SingpassKeys.Generate("dpop", DateTime.UtcNow),
            Issuer: metadata.Issuer,
            ClientId: config.ClientId!,
            RedirectUri: config.RedirectUri!);

        var form = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["scope"] = config.Scopes,
            ["state"] = session.State,
            ["nonce"] = session.Nonce,
            ["client_id"] = session.ClientId,
            ["redirect_uri"] = session.RedirectUri,
            ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(session.CodeVerifier))),
            ["code_challenge_method"] = "S256",
            ["authentication_context_type"] = AuthenticationContextType,
            ["client_assertion_type"] = ClientAssertionType,
            ["client_assertion"] = ClientAssertion(config, metadata.Issuer),
        };

        using var response = await PostFormAsync(metadata.PushedAuthorizationRequestEndpoint, form, session, ct);
        var requestUri = Required(response.RootElement, "request_uri");

        var url = $"{metadata.AuthorizationEndpoint}?client_id={Uri.EscapeDataString(session.ClientId)}" +
                  $"&request_uri={Uri.EscapeDataString(requestUri)}";
        return (url, session);
    }

    // --- Token exchange + ID token ---

    /// <summary>Exchanges the authorization code, then decrypts and validates the ID token.</summary>
    public async Task<SingpassIdentity> ExchangeCodeAsync(
        SingpassConfig config, SingpassAuthSession session, string code, CancellationToken ct = default)
    {
        var metadata = await GetMetadataAsync(config, ct);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = session.RedirectUri,
            ["code_verifier"] = session.CodeVerifier,
            ["client_assertion_type"] = ClientAssertionType,
            ["client_assertion"] = ClientAssertion(config, metadata.Issuer),
        };

        using var response = await PostFormAsync(metadata.TokenEndpoint, form, session, ct);
        var idToken = Required(response.RootElement, "id_token");

        var jws = Decrypt(config, idToken);
        return await ValidateAsync(config, metadata, session, jws, ct);
    }

    /// <summary>Opens the JWE with our encryption key; the result is the signed ID token (JWS).</summary>
    private string Decrypt(SingpassConfig config, string idToken)
    {
        var key = SingpassKeys.Parse(config.EncryptionKeyJwk!);
        try
        {
            var headers = JWT.Headers(idToken);
            if (headers.TryGetValue("alg", out var alg) && !$"{alg}".StartsWith("ECDH-ES", StringComparison.Ordinal))
                throw new SingpassException($"Unexpected ID token encryption ({alg}).");
            if (headers.TryGetValue("kid", out var kid) && $"{kid}" != key.Kid)
                logger.LogWarning("Singpass ID token encrypted for key {Kid}, current key is {Current}.", kid, key.Kid);

            using var ecdsa = SingpassKeys.ToECDsa(key);
            return JWT.Decode(idToken, ecdsa);
        }
        catch (JoseException ex)
        {
            logger.LogError(ex, "Singpass ID token could not be decrypted.");
            throw new SingpassException("The Singpass response could not be read.");
        }
    }

    private async Task<SingpassIdentity> ValidateAsync(
        SingpassConfig config, SingpassMetadata metadata, SingpassAuthSession session, string jws, CancellationToken ct)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudience = session.ClientId,
            ValidAlgorithms = SignatureAlgorithms,
            IssuerSigningKeys = await GetSigningKeysAsync(metadata, refresh: false, ct),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var result = await Tokens.ValidateTokenAsync(jws, parameters);
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Singpass rotated its keys: reload the JWKS once.
            parameters.IssuerSigningKeys = await GetSigningKeysAsync(metadata, refresh: true, ct);
            result = await Tokens.ValidateTokenAsync(jws, parameters);
        }
        if (!result.IsValid)
        {
            logger.LogError(result.Exception, "Singpass ID token rejected.");
            throw new SingpassException("The Singpass response could not be verified.");
        }

        var token = (JsonWebToken)result.SecurityToken;
        if (!token.TryGetPayloadValue<string>("nonce", out var nonce) || nonce != session.Nonce)
            throw new SingpassException("The Singpass response does not match this sign-in attempt.");

        // sub_attributes (name, email…) is a nested object: read it from the raw payload.
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(token.EncodedPayload));
        string? name = null, email = null;
        if (payload.RootElement.TryGetProperty("sub_attributes", out var attributes) &&
            attributes.ValueKind == JsonValueKind.Object)
        {
            name = OptionalString(attributes, "name");
            email = OptionalString(attributes, "email");
        }

        return new SingpassIdentity(token.Subject, name, email);
    }

    private async Task<IList<SecurityKey>> GetSigningKeysAsync(SingpassMetadata metadata, bool refresh, CancellationToken ct)
    {
        var key = $"singpass:jwks:{metadata.JwksUri}";
        if (!refresh && cache.TryGetValue(key, out IList<SecurityKey>? cached) && cached is not null)
            return cached;

        var json = await http.GetStringAsync(metadata.JwksUri, ct);
        var keys = new JsonWebKeySet(json).Keys
            .Where(k => k.Use is null or JsonWebKeyUseNames.Sig)
            .Cast<SecurityKey>()
            .ToList();
        cache.Set(key, (IList<SecurityKey>)keys, MetadataCacheDuration);
        return keys;
    }

    // --- Proofs ---

    /// <summary><c>private_key_jwt</c>: iss = sub = client_id, aud = the Singpass issuer.</summary>
    private static string ClientAssertion(SingpassConfig config, string issuer)
    {
        var jwk = SingpassKeys.Parse(config.SigningKeyJwk!);
        var now = DateTimeOffset.UtcNow;
        var key = new ECDsaSecurityKey(SingpassKeys.ToECDsa(jwk)) { KeyId = jwk.Kid };
        return Tokens.CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "JWT",
            SigningCredentials = new SigningCredentials(key, SingpassKeys.SigningAlgorithm),
            Claims = new Dictionary<string, object>
            {
                ["iss"] = config.ClientId!,
                ["sub"] = config.ClientId!,
                ["aud"] = issuer,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.Add(ProofLifetime).ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString(),
            },
        });
    }

    /// <summary>DPoP proof (RFC 9449) for one request, signed with the session's ephemeral key.</summary>
    private static string DpopProof(SingpassAuthSession session, string method, string url)
    {
        var jwk = SingpassKeys.Parse(session.DpopKeyJwk);
        var now = DateTimeOffset.UtcNow;
        var key = new ECDsaSecurityKey(SingpassKeys.ToECDsa(jwk)); // no kid: the key travels in the header
        return Tokens.CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "dpop+jwt",
            SigningCredentials = new SigningCredentials(key, SingpassKeys.SigningAlgorithm),
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                ["jwk"] = new Dictionary<string, object> { ["kty"] = jwk.Kty, ["crv"] = jwk.Crv, ["x"] = jwk.X, ["y"] = jwk.Y },
            },
            Claims = new Dictionary<string, object>
            {
                ["htm"] = method,
                ["htu"] = url,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.Add(ProofLifetime).ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString(),
            },
        });
    }

    // --- HTTP ---

    private async Task<JsonDocument> PostFormAsync(
        string url, Dictionary<string, string> form, SingpassAuthSession session, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("DPoP", DpopProof(session, "POST", url));
        using var response = await SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadError(body);
            logger.LogError("Singpass {Url} answered {Status}: {Error}", url, (int)response.StatusCode, error ?? body);
            throw new SingpassException(error is null
                ? "Singpass is unavailable. Please try again later."
                : $"Singpass rejected the request ({error.Split(':')[0]}).");
        }
        return JsonDocument.Parse(body);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Singpass {Url} answered {Status}.", url, (int)response.StatusCode);
            throw new SingpassException("Singpass is unavailable. Please try again later.");
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Singpass {Url} unreachable.", request.RequestUri);
            throw new SingpassException("Singpass is unreachable. Please try again later.");
        }
    }

    /// <summary><c>error: error_description</c> of an OAuth error body, if it is one.</summary>
    private static string? TryReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var error = OptionalString(doc.RootElement, "error");
            var description = OptionalString(doc.RootElement, "error_description");
            return error is null ? null : description is null ? error : $"{error}: {description}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Required(JsonElement element, string name)
        => OptionalString(element, name)
           ?? throw new SingpassException($"Singpass response is missing '{name}'.");

    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>URL-safe random string (state, nonce, PKCE verifier: 43-128 chars of [A-Za-z0-9-_]).</summary>
    private static string RandomToken(int bytes) => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(bytes));
}
