using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Managers;

namespace Torii.Api.Controllers;

/// <summary>
/// Uploaded avatars, served anonymously: an <c>&lt;img&gt;</c> cannot send the Bearer token.
/// URLs carry a version (<c>?v=</c>) changed on every upload, so responses are cached for long.
/// </summary>
[AllowAnonymous]
[Route("api/users/{id:long}/avatar")]
public class UserAvatarController(ProfileManager profiles) : ToriController
{
    [HttpGet]
    [ResponseCache(Duration = 60 * 60 * 24 * 30, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var avatar = await profiles.OpenAvatarAsync(id, ct);
        if (avatar is null)
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(avatar.Content, avatar.ContentType);
    }
}
