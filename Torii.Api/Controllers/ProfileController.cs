using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Services;

namespace Torii.Api.Controllers;

/// <summary>The signed-in user's own profile (identity, email, password, avatar, sessions).</summary>
[Authorize]
[Route("api/profile")]
public class ProfileController(ProfileManager profiles, IAuthService auth) : ToriController
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
        => Ok(await profiles.GetAsync(CurrentUserId, ct));

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request, CancellationToken ct)
        => Ok(await profiles.UpdateAsync(CurrentUserId, request, ct));

    /// <summary>Changes the email (current password required; 400 when wrong, 409 when taken).</summary>
    [HttpPut("email")]
    public async Task<ActionResult<ProfileDto>> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
        => Ok(await profiles.ChangeEmailAsync(CurrentUserId, request, ct));

    /// <summary>Changes the password: every session is revoked, a fresh one is returned.</summary>
    [HttpPut("password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => Ok(await auth.ChangePasswordAsync(CurrentUserId, request, ct));

    /// <summary>Uploads a new avatar (multipart field <c>file</c>: PNG, JPEG, WebP or GIF, 5 MB max).</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar(IFormFile file, CancellationToken ct)
        => Ok(await profiles.UploadAvatarAsync(CurrentUserId, file, ct));

    /// <summary>Back to the default avatar.</summary>
    [HttpDelete("avatar")]
    public async Task<ActionResult<ProfileDto>> RemoveAvatar(CancellationToken ct)
        => Ok(await profiles.RemoveAvatarAsync(CurrentUserId, ct));

    /// <summary>Signs out of all devices (every refresh token revoked, including the current one).</summary>
    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken ct)
    {
        await auth.RevokeAllSessionsAsync(CurrentUserId, ct);
        return NoContent();
    }
}
