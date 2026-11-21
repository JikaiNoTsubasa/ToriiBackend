using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers;

[Authorize]
[Route("api/tickets")]
public class TicketController(TicketManager tickets) : ToriController
{
    /// <summary>Maximum size of one upload request (all files together).</summary>
    private const long MaxUploadRequestBytes = 100L * 1024 * 1024;

    /// <summary>
    /// One page of the tickets visible to the signed-in user (through their customers), newest
    /// first. <c>search</c> matches the number or the title; <c>status</c> (repeatable, e.g.
    /// <c>?status=NEW&amp;status=IN_PROGRESS</c>) keeps tickets where at least one of the user's
    /// channels has one of them — absent = any status. <c>pageSize</c> is capped at 100.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery(Name = "status")] TicketStatus[]? statuses,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = TicketManager.DefaultPageSize,
        CancellationToken ct = default)
        => Ok(await tickets.ListVisibleAsync(
            CurrentUserId,
            new TicketListQuery(search, statuses is { Length: > 0 } ? statuses : null, page, pageSize),
            ct));

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
    /// Uploads files to a channel (multipart field <c>files</c>, several allowed; each file is
    /// limited by <c>Storage:MaxFileSizeMb</c>). Recorded in the channel's activity feed.
    /// </summary>
    [HttpPost("{number}/channels/{channelId:long}/files")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    public async Task<ActionResult<IReadOnlyList<FileRefDto>>> UploadFiles(
        string number, long channelId, [FromForm] List<IFormFile> files, CancellationToken ct)
        => Ok(await tickets.UploadFilesAsync(number, channelId, files, CurrentUserId, ct));

    /// <summary>
    /// Downloads a file of a channel (participants only). Always served as an attachment with
    /// <c>nosniff</c>, so uploaded HTML / scripts are never rendered by the browser.
    /// </summary>
    [HttpGet("{number}/channels/{channelId:long}/files/{fileId:long}")]
    public async Task<IActionResult> DownloadFile(string number, long channelId, long fileId, CancellationToken ct)
    {
        var file = await tickets.OpenFileAsync(number, channelId, fileId, CurrentUserId, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, "application/octet-stream", file.FileName);
    }

    /// <summary>
    /// Forwards a file to another channel of the ticket (only the file id is linked): an existing
    /// channel, or the channel between two customers, created if needed (edge required).
    /// </summary>
    [HttpPost("{number}/channels/{channelId:long}/files/{fileId:long}/forward")]
    public async Task<ActionResult<ForwardFileResultDto>> ForwardFile(
        string number, long channelId, long fileId, ForwardFileRequest request, CancellationToken ct)
        => Ok(await tickets.ForwardFileAsync(number, channelId, fileId, request, CurrentUserId, ct));

    /// <summary>
    /// Opens a new channel on the ticket towards another customer allowed by the ticket
    /// edges, with a first message. Returns the updated ticket details.
    /// </summary>
    [HttpPost("{number}/channels")]
    public async Task<ActionResult<TicketDetailDto>> OpenChannel(
        string number, OpenChannelRequest request, CancellationToken ct)
        => Ok(await tickets.OpenChannelAsync(number, request, CurrentUserId, ct));
}
