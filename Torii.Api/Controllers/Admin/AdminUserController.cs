using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers.Admin;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin/users")]
public class AdminUserController(UserManager users) : ToriController
{
    /// <summary>User search (email or name), 20 results max.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Search([FromQuery] string? search, CancellationToken ct)
        => Ok(await users.SearchAsync(search, ct));
}
