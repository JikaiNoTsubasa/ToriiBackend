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

    /// <summary>
    /// Customers the signed-in user may send tickets from, each with its allowed
    /// targets (active ticket edges).
    /// </summary>
    [HttpGet("send-options")]
    public async Task<ActionResult<IReadOnlyList<TicketSendOptionDto>>> SendOptions(CancellationToken ct)
        => Ok(await tickets.GetSendOptionsAsync(CurrentUserId, ct));

    /// <summary>
    /// Creates a ticket, its channel between the source and target customers and the
    /// first message (403 if the user is not in the source customer or no edge allows it).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TicketListItemDto>> Create(CreateTicketRequest request, CancellationToken ct)
        => Ok(await tickets.CreateAsync(request, CurrentUserId, ct));

    /// <summary>
    /// Changes the status of one channel of the ticket (members of a customer of that
    /// channel only; 404 otherwise). Returns the updated listed ticket.
    /// </summary>
    [HttpPut("{number}/channels/{channelId:long}/status")]
    public async Task<ActionResult<TicketListItemDto>> UpdateChannelStatus(
        string number, long channelId, UpdateChannelStatusRequest request, CancellationToken ct)
        => Ok(await tickets.UpdateChannelStatusAsync(number, channelId, request.Status!.Value, CurrentUserId, ct));
}
