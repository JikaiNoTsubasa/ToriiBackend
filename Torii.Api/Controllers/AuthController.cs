using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Services;

namespace Torii.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    // Business errors bubble up as exceptions and are converted
    // into ProblemDetails responses by the GlobalExceptionHandler.
    /// <summary>Account request: 202, the account waits for an administrator's approval.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegistrationResponse>> Register(RegisterRequest request, CancellationToken ct)
        => Accepted(await auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await auth.RefreshAsync(request.RefreshToken, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");

        if (id is null || !long.TryParse(id, out var userId))
            return Unauthorized();

        var user = await auth.GetByIdAsync(userId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
