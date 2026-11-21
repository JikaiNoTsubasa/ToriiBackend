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

    /// <summary>
    /// HTML content. For a forwarded message, the forwarder's optional comment (may be
    /// empty): the forwarded content itself is read from <see cref="ForwardedFrom"/>.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>Original message when this one is a forward (always the root original, never a chain).</summary>
    public long? ForwardedFromId { get; set; }
    public TicketMessage? ForwardedFrom { get; set; }
}
