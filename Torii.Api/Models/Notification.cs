namespace Torii.Api.Models;

/// <summary>Kind of in-app notification.</summary>
public enum NotificationType
{
    /// <summary>Someone posted (or forwarded) a message in a channel the user takes part in.</summary>
    MESSAGE_POSTED,
}

/// <summary>
/// In-app notification of a user. Unread MESSAGE_POSTED notifications are coalesced per
/// channel: a new message bumps <see cref="Count"/>, <see cref="LastMessage"/>,
/// <see cref="Actor"/> and <see cref="CreatedAt"/> instead of adding a row.
/// </summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>Recipient.</summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationType Type { get; set; }

    public string TicketNumber { get; set; } = null!;
    public Ticket Ticket { get; set; } = null!;

    public long TicketChannelId { get; set; }
    public TicketChannel TicketChannel { get; set; } = null!;

    /// <summary>Latest message notified (shown as the preview).</summary>
    public long LastMessageId { get; set; }
    public TicketMessage LastMessage { get; set; } = null!;

    /// <summary>Author of the latest message.</summary>
    public long ActorId { get; set; }
    public User Actor { get; set; } = null!;

    /// <summary>Messages covered by this notification since it was last read.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Time of the latest message notified.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null while unread.</summary>
    public DateTime? ReadAt { get; set; }
}
