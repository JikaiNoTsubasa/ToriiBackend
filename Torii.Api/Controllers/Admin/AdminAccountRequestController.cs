using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers.Admin;

/// <summary>Accounts requested through /register, waiting for an administrator's decision.</summary>
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin/account-requests")]
public class AdminAccountRequestController(AccountRequestManager requests) : ToriController
{
    /// <summary>Pending requests, oldest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountRequestDto>>> List(CancellationToken ct)
        => Ok(await requests.ListAsync(ct));

    /// <summary>Number of pending requests (menu badge).</summary>
    [HttpGet("count")]
    public async Task<ActionResult<int>> Count(CancellationToken ct)
        => Ok(await requests.CountAsync(ct));

    /// <summary>Activates the account: the user can sign in.</summary>
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        await requests.ApproveAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Declines the request: the account is marked REJECTED and cannot sign in.</summary>
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken ct)
    {
        await requests.RejectAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
