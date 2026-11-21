using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;

namespace Torii.Api.Controllers;

/// <summary>In-app notifications of the signed-in user (new messages in their channels).</summary>
[Authorize]
[Route("api/notifications")]
public class NotificationController(NotificationManager notifications) : ToriController
{
    /// <summary>Latest notifications, newest first, with the unread total.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> List(
        [FromQuery] bool unreadOnly = false, [FromQuery] int? take = null, CancellationToken ct = default)
        => Ok(await notifications.ListAsync(CurrentUserId, unreadOnly, take, ct));

    /// <summary>Unread total + latest unread time (cheap, meant for polling).</summary>
    [HttpGet("unread")]
    public async Task<ActionResult<UnreadSummaryDto>> Unread(CancellationToken ct)
        => Ok(await notifications.GetUnreadSummaryAsync(CurrentUserId, ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    /// <summary>Marks the notifications of a channel read (sent when the user views it).</summary>
    [HttpPost("channels/{channelId:long}/read")]
    public async Task<IActionResult> MarkChannelRead(long channelId, CancellationToken ct)
    {
        await notifications.MarkChannelReadAsync(CurrentUserId, channelId, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(CurrentUserId, ct);
        return NoContent();
    }
}
