namespace Torii.Api.Models;

/// <summary>Kind of event recorded in a channel's activity feed.</summary>
public enum TicketActivityType
{
    /// <summary>The channel was opened (ticket creation, "open channel", or a forward creating it).</summary>
    CHANNEL_OPENED,

    /// <summary>Status changed from <see cref="TicketActivity.FromStatus"/> to <see cref="TicketActivity.ToStatus"/>.</summary>
    STATUS_CHANGED,

    /// <summary>A file was uploaded to the channel (<see cref="TicketActivity.ChannelFile"/>).</summary>
    FILE_UPLOADED,

    /// <summary>A file was forwarded into the channel from another channel of the ticket.</summary>
    FILE_FORWARDED,
}

/// <summary>
/// Immutable event of a channel's activity feed (who did what, when). Shown interleaved with
/// the messages. Only the fields relevant to <see cref="Type"/> are set.
/// </summary>
public class TicketActivity
{
    public long Id { get; set; }

    public long TicketChannelId { get; set; }
    public TicketChannel TicketChannel { get; set; } = null!;

    public TicketActivityType Type { get; set; }

    public long ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>STATUS_CHANGED: previous status (null when unknown, e.g. backfilled history).</summary>
    public TicketStatus? FromStatus { get; set; }

    /// <summary>STATUS_CHANGED: new status.</summary>
    public TicketStatus? ToStatus { get; set; }

    /// <summary>FILE_UPLOADED / FILE_FORWARDED: the file link concerned.</summary>
    public long? ChannelFileId { get; set; }
    public ChannelFile? ChannelFile { get; set; }
}
