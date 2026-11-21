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
    /// <summary>
    /// Users (email or name search). <c>activeOnly</c> hides disabled users;
    /// <c>take</c> limits the results (default 100, max 500).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> List(
        [FromQuery] string? search, [FromQuery] bool activeOnly, [FromQuery] int? take, CancellationToken ct)
        => Ok(await users.ListAsync(search, activeOnly, take, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminUserDto>> Get(long id, CancellationToken ct)
        => Ok(await users.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create(AdminUserCreateRequest request, CancellationToken ct)
    {
        var created = await users.CreateAsync(request, CurrentUserId, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminUserDto>> Update(long id, AdminUserUpdateRequest request, CancellationToken ct)
        => Ok(await users.UpdateAsync(id, request, CurrentUserId, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await users.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
