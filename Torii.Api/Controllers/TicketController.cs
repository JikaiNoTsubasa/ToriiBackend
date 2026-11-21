using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;

namespace Torii.Api.Controllers;

[Authorize]
[Route("api/tickets")]
public class TicketController(TicketManager tickets) : ToriController
{
    /// <summary>Tickets visible to the signed-in user (through their customers).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> List(CancellationToken ct)
        => Ok(await tickets.ListVisibleAsync(CurrentUserId, ct));
}
