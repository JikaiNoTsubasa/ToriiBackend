using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers.Admin;

/// <summary>Ticket configuration: which customer may send tickets to which other customer.</summary>
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin/ticket-edges")]
public class AdminTicketEdgeController(TicketEdgeManager edges) : ToriController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketEdgeDto>>> List(CancellationToken ct)
        => Ok(await edges.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TicketEdgeDto>> Create(TicketEdgeCreateRequest request, CancellationToken ct)
        => Ok(await edges.CreateAsync(request, CurrentUserId, ct));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<TicketEdgeDto>> Update(long id, TicketEdgeUpdateRequest request, CancellationToken ct)
        => Ok(await edges.UpdateAsync(id, request, CurrentUserId, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await edges.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
