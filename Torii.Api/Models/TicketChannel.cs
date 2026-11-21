namespace Torii.Api.Models;

/// <summary>
/// (Bidirectional) exchange channel of a ticket between customers. The parties
/// are held by <see cref="Participants"/> (one row per customer).
/// </summary>
public class TicketChannel : Entity
{
    public string TicketNumber { get; set; } = null!;
    public Ticket Ticket { get; set; } = null!;

    public ICollection<ChannelParticipant> Participants { get; set; } = [];

    /// <summary>Status of this channel (independent from the ticket's other channels).</summary>
    public TicketStatus Status { get; set; } = TicketStatus.NEW;

    /// <summary>Last status change (null while the channel is still in its initial status).</summary>
    public DateTime? StatusChangedAt { get; set; }
    public long? StatusChangedById { get; set; }

    /// <summary>Messages exchanged in the channel, oldest first by <c>CreatedAt</c>.</summary>
    public ICollection<TicketMessage> Messages { get; set; } = [];
}
