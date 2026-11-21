using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// In-app notifications: created when a message lands in a channel (reply or forward), for
/// every member of the channel's customers but its author; listed / counted / marked read by
/// their recipient. A notification is only shown while its channel is still visible to the
/// recipient (same rule as the tickets), so a removed member stops seeing message previews.
/// </summary>
public partial class NotificationManager(ToriiDbContext db) : ToriManager(db)
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;
    private const int PreviewLength = 140;

    /// <summary>The user's notifications whose channel the user can still see.</summary>
    private IQueryable<Notification> QueryFor(long userId) =>
        _dbContext.Notifications.Where(n =>
            n.UserId == userId &&
            !n.TicketChannel.IsDeleted &&
            n.TicketChannel.Participants.Any(p =>
                !p.Customer.IsDeleted && p.Customer.Members.Any(u => u.Id == userId)));

    /// <summary>
    /// Notifies the message <paramref name="messageId"/>, just saved in <paramref name="channelId"/>
    /// by <paramref name="authorId"/>: an unread notification of the same channel is bumped,
    /// otherwise one is created. The author's own unread notifications of the channel are
    /// marked read (replying means the channel was read).
    /// </summary>
    public async Task NotifyMessageAsync(
        string ticketNumber, long channelId, long messageId, long authorId, CancellationToken ct = default)
    {
        var recipientIds = await _dbContext.ChannelParticipants
            .Where(p => p.TicketChannelId == channelId && !p.Customer.IsDeleted)
            .SelectMany(p => p.Customer.Members)
            .Where(u => u.Id != authorId && u.IsActive && !u.IsDeleted)
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

        var unread = await _dbContext.Notifications
            .Where(n => n.TicketChannelId == channelId && n.ReadAt == null && n.Type == NotificationType.MESSAGE_POSTED)
            .Where(n => recipientIds.Contains(n.UserId) || n.UserId == authorId)
            .ToListAsync(ct);
        var unreadByUser = unread.ToDictionary(n => n.UserId);

        var now = DateTime.UtcNow;
        if (unreadByUser.TryGetValue(authorId, out var own))
            own.ReadAt = now;

        foreach (var userId in recipientIds)
        {
            if (unreadByUser.TryGetValue(userId, out var existing))
            {
                existing.Count++;
                existing.LastMessageId = messageId;
                existing.ActorId = authorId;
                existing.CreatedAt = now;
            }
            else
            {
                _dbContext.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.MESSAGE_POSTED,
                    TicketNumber = ticketNumber,
                    TicketChannelId = channelId,
                    LastMessageId = messageId,
                    ActorId = authorId,
                    CreatedAt = now,
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Latest notifications, newest first (<paramref name="unreadOnly"/> hides read ones).</summary>
    public async Task<NotificationListDto> ListAsync(long userId, bool unreadOnly, int? take, CancellationToken ct = default)
    {
        var query = QueryFor(userId).AsNoTracking();
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var rows = await query
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(Math.Clamp(take ?? DefaultTake, 1, MaxTake))
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.TicketNumber,
                n.Ticket.Title,
                n.TicketChannelId,
                Counterparts = n.TicketChannel.Participants
                    .Where(p => !p.Customer.Members.Any(u => u.Id == userId))
                    .Select(p => p.Customer.Name)
                    .ToList(),
                Actor = new UserRefDto(n.Actor.Id, n.Actor.DisplayName, n.Actor.AvatarUrl),
                n.LastMessage.Content,
                ForwardedContent = n.LastMessage.ForwardedFrom == null ? null : n.LastMessage.ForwardedFrom.Content,
                n.Count,
                n.CreatedAt,
                n.ReadAt,
            })
            .ToListAsync(ct);

        var items = rows
            .Select(n =>
            {
                var preview = Excerpt(n.Content);
                if (preview.Length == 0 && n.ForwardedContent is not null)
                    preview = Excerpt(n.ForwardedContent);
                return new NotificationDto(
                    n.Id, n.Type, n.TicketNumber, n.Title, n.TicketChannelId,
                    string.Join(", ", n.Counterparts.Order()),
                    n.Actor, preview, n.ForwardedContent is not null, n.Count, n.CreatedAt, n.ReadAt);
            })
            .ToList();

        return new NotificationListDto(items, await CountUnreadAsync(userId, ct));
    }

    public Task<int> CountUnreadAsync(long userId, CancellationToken ct = default)
        => QueryFor(userId).CountAsync(n => n.ReadAt == null, ct);

    public async Task<UnreadSummaryDto> GetUnreadSummaryAsync(long userId, CancellationToken ct = default)
    {
        var unread = QueryFor(userId).Where(n => n.ReadAt == null);
        return new UnreadSummaryDto(
            await unread.CountAsync(ct),
            await unread.MaxAsync(n => (DateTime?)n.CreatedAt, ct));
    }

    /// <summary>Marks one notification read (404 when it is not the user's).</summary>
    public async Task MarkReadAsync(long userId, long id, CancellationToken ct = default)
    {
        var updated = await _dbContext.Notifications
            .Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, n => n.ReadAt ?? DateTime.UtcNow), ct);
        if (updated == 0)
            throw Exceptions.NotFoundException.For("Notification", id);
    }

    /// <summary>Marks the user's notifications of a channel read (when the channel is viewed).</summary>
    public Task MarkChannelReadAsync(long userId, long channelId, CancellationToken ct = default)
        => _dbContext.Notifications
            .Where(n => n.UserId == userId && n.TicketChannelId == channelId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

    public Task MarkAllReadAsync(long userId, CancellationToken ct = default)
        => _dbContext.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

    /// <summary>Plain-text excerpt of rich text (tags stripped, whitespace collapsed).</summary>
    private static string Excerpt(string html)
    {
        var text = Whitespace().Replace(WebUtility.HtmlDecode(HtmlTag().Replace(html, " ")), " ").Trim();
        return text.Length > PreviewLength ? text[..PreviewLength].TrimEnd() + "…" : text;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
