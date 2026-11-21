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
}
