namespace Torii.Api.Models;

/// <summary>
/// Customer taking part in a <see cref="TicketChannel"/>. The channel is
/// bidirectional: all participants are equivalent. Composite key
/// (channel, customer): a customer appears only once per channel.
/// </summary>
public class ChannelParticipant
{
    public long TicketChannelId { get; set; }
    public TicketChannel TicketChannel { get; set; } = null!;

    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}
