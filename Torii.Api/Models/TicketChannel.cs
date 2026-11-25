namespace Torii.Api.Models;

/// <summary>
/// (Bidirectional) exchange channel of a ticket between customers. The parties
/// are held by <see cref="Participants"/> (one row per customer).
/// An <see cref="IsInternal"/> channel is a customer's private thread on the ticket instead:
/// one participant (that customer), seen by its members only, without status.
/// </summary>
public class TicketChannel : Entity
{
    public string TicketNumber { get; set; } = null!;
    public Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Internal thread of its single participant (at most one per ticket and customer): not an
    /// exchange, so it neither grants access to the ticket nor counts in statuses / dashboards.
    /// </summary>
    public bool IsInternal { get; set; }

    public ICollection<ChannelParticipant> Participants { get; set; } = [];

    /// <summary>Status of this channel (independent from the ticket's other channels).</summary>
    public TicketStatus Status { get; set; } = TicketStatus.NEW;

    /// <summary>Last status change (null while the channel is still in its initial status).</summary>
    public DateTime? StatusChangedAt { get; set; }
    public long? StatusChangedById { get; set; }

    /// <summary>Messages exchanged in the channel, oldest first by <c>CreatedAt</c>.</summary>
    public ICollection<TicketMessage> Messages { get; set; } = [];

    /// <summary>Files attached to the channel (uploaded or forwarded).</summary>
    public ICollection<ChannelFile> Files { get; set; } = [];

    /// <summary>Activity feed of the channel (status changes, files, …).</summary>
    public ICollection<TicketActivity> Activities { get; set; } = [];
}
