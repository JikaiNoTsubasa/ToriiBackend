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

    /// <summary>
    /// Ticket details as seen by the signed-in user: only their channels, with the messages
    /// (404 if they take part in no channel of the ticket).
    /// </summary>
    [HttpGet("{number}")]
    public async Task<ActionResult<TicketDetailDto>> Get(string number, CancellationToken ct)
        => Ok(await tickets.GetDetailAsync(number, CurrentUserId, ct));

    /// <summary>Replies in one channel of the ticket (participants only; 404 otherwise).</summary>
    [HttpPost("{number}/channels/{channelId:long}/messages")]
    public async Task<ActionResult<TicketMessageDto>> PostMessage(
        string number, long channelId, PostMessageRequest request, CancellationToken ct)
        => Ok(await tickets.PostMessageAsync(number, channelId, request, CurrentUserId, ct));

    /// <summary>
    /// Forwards a message to another channel of the ticket: an existing one
    /// (<c>targetChannelId</c>) or the channel between <c>sourceCustomerId</c> and
    /// <c>targetCustomerId</c>, created if needed (edge required).
    /// </summary>
    [HttpPost("{number}/channels/{channelId:long}/messages/{messageId:long}/forward")]
    public async Task<ActionResult<ForwardResultDto>> ForwardMessage(
        string number, long channelId, long messageId, ForwardMessageRequest request, CancellationToken ct)
        => Ok(await tickets.ForwardMessageAsync(number, channelId, messageId, request, CurrentUserId, ct));

    /// <summary>
    /// Opens a new channel on the ticket towards another customer allowed by the ticket
    /// edges, with a first message. Returns the updated ticket details.
    /// </summary>
    [HttpPost("{number}/channels")]
    public async Task<ActionResult<TicketDetailDto>> OpenChannel(
        string number, OpenChannelRequest request, CancellationToken ct)
        => Ok(await tickets.OpenChannelAsync(number, request, CurrentUserId, ct));
}
