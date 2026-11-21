namespace Torii.Api.Models;

/// <summary>
/// Message posted in a <see cref="TicketChannel"/>. The first message of a channel
/// is the content sent when the ticket was created. <see cref="Content"/> is HTML
/// produced by the rich text editor: sanitize it before any use outside the app.
/// </summary>
public class TicketMessage : Entity
{
    public long TicketChannelId { get; set; }
    public TicketChannel TicketChannel { get; set; } = null!;

    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public string Content { get; set; } = null!;
}
