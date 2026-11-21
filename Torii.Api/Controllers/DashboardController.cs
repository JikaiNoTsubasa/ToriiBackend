using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;

namespace Torii.Api.Controllers;

/// <summary>Home dashboard of the signed-in user (over the channels they can see).</summary>
[Authorize]
[Route("api/dashboard")]
public class DashboardController(TicketManager tickets) : ToriController
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
        => Ok(await tickets.GetDashboardAsync(CurrentUserId, ct));
}
