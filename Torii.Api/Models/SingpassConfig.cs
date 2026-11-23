using System.ComponentModel.DataAnnotations.Schema;

namespace Torii.Api.Models;

/// <summary>Singpass environment the application is registered on.</summary>
public enum SingpassEnvironment
{
    STAGING,
    PRODUCTION,
}

/// <summary>
/// Singpass (FAPI 2.0) relying party settings, edited by administrators. Single row
/// (<see cref="SingletonId"/>). The private keys are EC P-256 JWKs (JSON, private part
/// included); only their public part is ever served, by the JWKS endpoint registered with
/// Singpass.
/// </summary>
public class SingpassConfig
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Allows signing in and requesting an account with Singpass.</summary>
    public bool Enabled { get; set; }

    public SingpassEnvironment Environment { get; set; } = SingpassEnvironment.STAGING;

    /// <summary>App ID given by the Singpass Developer Portal.</summary>
    public string? ClientId { get; set; }

    /// <summary>Frontend callback registered with Singpass (e.g. https://host/auth/singpass/callback).</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Space-delimited scopes; must include <c>openid</c>.</summary>
    public string Scopes { get; set; } = DefaultScopes;

    public const string DefaultScopes = "openid name email";

    /// <summary>Private signing key (ES256) for the client assertions, as a JWK.</summary>
    public string? SigningKeyJwk { get; set; }

    /// <summary>Private decryption key (ECDH-ES+A256KW) of the ID tokens, as a JWK.</summary>
    public string? EncryptionKeyJwk { get; set; }

    public DateTime? KeysGeneratedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedById { get; set; }

    /// <summary>Development only: issuer of a local mock (<c>Singpass:IssuerOverride</c> setting).</summary>
    [NotMapped]
    public string? IssuerOverride { get; set; }

    /// <summary>Issuer of the environment (discovery at <c>{issuer}/.well-known/openid-configuration</c>).</summary>
    public string Issuer => IssuerOverride ?? (Environment == SingpassEnvironment.PRODUCTION
        ? "https://id.singpass.gov.sg/fapi"
        : "https://stg-id.singpass.gov.sg/fapi");

    /// <summary>Everything needed to talk to Singpass is set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(RedirectUri) &&
        SigningKeyJwk is not null &&
        EncryptionKeyJwk is not null;

    /// <summary>Singpass buttons are usable.</summary>
    public bool IsAvailable => Enabled && IsConfigured;
}
