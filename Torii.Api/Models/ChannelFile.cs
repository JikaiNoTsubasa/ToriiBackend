namespace Torii.Api.Models;

/// <summary>
/// Attachment of a <see cref="StoredFile"/> to a <see cref="TicketChannel"/>: created on upload,
/// and again (same file, new link) when the file is forwarded to another channel.
/// <c>CreatedById</c> / <c>CreatedAt</c> tell who added it and when.
/// </summary>
public class ChannelFile : Entity
{
    public long TicketChannelId { get; set; }
    public TicketChannel TicketChannel { get; set; } = null!;

    public long StoredFileId { get; set; }
    public StoredFile StoredFile { get; set; } = null!;

    /// <summary>Channel the file was forwarded from (null for an upload).</summary>
    public long? ForwardedFromChannelId { get; set; }
}
