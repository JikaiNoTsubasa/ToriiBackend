using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Services.Singpass;

namespace Torii.Api.Controllers;

/// <summary>
/// Singpass sign-in / account request (anonymous). The frontend calls <c>start</c>, sends the
/// browser to Singpass, then posts what Singpass returned to its callback page to <c>callback</c>.
/// </summary>
[Route("api/auth/singpass")]
public class SingpassAuthController(SingpassAuthService singpass, SingpassConfigManager configs) : ToriController
{
    /// <summary>Whether the Singpass buttons are usable.</summary>
    [HttpGet]
    public async Task<ActionResult<SingpassStatusDto>> Status(CancellationToken ct)
        => Ok(await singpass.GetStatusAsync(ct));

    /// <summary>
    /// Our public keys (signing + encryption), to register as the JWKS URL in the Singpass
    /// Developer Portal. Must stay publicly reachable.
    /// </summary>
    [HttpGet("jwks")]
    public async Task<IActionResult> Jwks(CancellationToken ct)
    {
        var jwks = await configs.GetPublicJwksAsync(ct);
        // Serialized without null members: a published key must not carry a "d" member at all.
        return jwks is null ? NotFound() : Content(SingpassKeys.Serialize(jwks), "application/jwk-set+json");
    }

    [HttpPost("start")]
    public async Task<ActionResult<SingpassStartResponse>> Start(CancellationToken ct)
        => Ok(await singpass.StartAsync(ct));

    [HttpPost("callback")]
    public async Task<ActionResult<SingpassCallbackResponse>> Callback(SingpassCallbackRequest request, CancellationToken ct)
        => Ok(await singpass.CallbackAsync(request, ct));

    /// <summary>Account request after a Singpass authentication: 202, pending approval.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegistrationResponse>> Register(SingpassRegisterRequest request, CancellationToken ct)
        => Accepted(await singpass.RegisterAsync(request, ct));
}
