using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>Public: whether the Singpass buttons are usable (enabled and fully configured).</summary>
public record SingpassStatusDto(bool Enabled);

/// <summary>URL to send the browser to (Singpass authorization page).</summary>
public record SingpassStartResponse(string AuthorizationUrl);

/// <summary>Query parameters Singpass sent back to the frontend callback page.</summary>
public record SingpassCallbackRequest(string? Code, string? State, string? Iss, string? Error);

/// <summary>Outcome of the callback.</summary>
public enum SingpassCallbackOutcome
{
    /// <summary>A linked, approved account: <see cref="SingpassCallbackResponse.Session"/> is set.</summary>
    SIGNED_IN,

    /// <summary>No account is linked to this Singpass identity: <see cref="SingpassCallbackResponse.Registration"/> is set.</summary>
    REGISTRATION_REQUIRED,
}

/// <summary>
/// Identity to complete the account request with: <see cref="Token"/> proves the Singpass
/// authentication (single use, 15 minutes); name / email prefill the form when Singpass gave them.
/// </summary>
public record SingpassRegistrationDto(string Token, string? DisplayName, string? Email);

public record SingpassCallbackResponse(
    SingpassCallbackOutcome Outcome,
    AuthResponse? Session,
    SingpassRegistrationDto? Registration);

/// <summary>Account request after a Singpass authentication (no password: Singpass only).</summary>
public record SingpassRegisterRequest(
    [Required] string Token,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string DisplayName,
    [MaxLength(128)] string? Position = null);

/// <summary>Singpass settings as shown to administrators: public key ids only, never the private keys.</summary>
public record SingpassConfigDto(
    bool Enabled,
    SingpassEnvironment Environment,
    string Issuer,
    string? ClientId,
    string? RedirectUri,
    string Scopes,
    string? SigningKeyId,
    string? EncryptionKeyId,
    DateTime? KeysGeneratedAt,
    bool IsConfigured,
    DateTime? UpdatedAt);

public record SingpassConfigUpdateRequest(
    bool Enabled,
    SingpassEnvironment Environment,
    [MaxLength(64)] string? ClientId,
    [MaxLength(512)] string? RedirectUri,
    [Required, MaxLength(256)] string Scopes);

/// <summary>Endpoints read from the Singpass discovery document (connection test).</summary>
public record SingpassTestResultDto(
    string Issuer,
    string AuthorizationEndpoint,
    string PushedAuthorizationRequestEndpoint,
    string TokenEndpoint,
    string JwksUri);
