using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>Ticket as listed for the signed-in user (only the channels they take part in).</summary>
public record TicketListItemDto(
    string Number,
    string Title,
    TicketType Type,
    TicketPriority Priority,
    UserRefDto? CreatedBy,
    DateTime CreatedAt,
    IReadOnlyList<TicketChannelDto> Channels);

/// <summary>
/// Ticket list query: <see cref="Search"/> matches the number or the title (case-insensitive);
/// <see cref="Statuses"/> keeps tickets where at least one of the user's channels has one of
/// them (null = any status); <see cref="Types"/> / <see cref="Priorities"/> keep tickets of one
/// of them (null = any).
/// </summary>
public record TicketListQuery(
    string? Search,
    IReadOnlyCollection<TicketStatus>? Statuses,
    IReadOnlyCollection<TicketType>? Types,
    IReadOnlyCollection<TicketPriority>? Priorities,
    int Page,
    int PageSize);

/// <summary>Type / priority change of a ticket (participants of any of its channels).</summary>
public record UpdateTicketClassificationRequest([Required] TicketType? Type, [Required] TicketPriority? Priority);

/// <summary>Minimal user reference for display (name + avatar).</summary>
public record UserRefDto(long Id, string DisplayName, string AvatarUrl);

public record TicketChannelDto(
    long Id,
    TicketStatus Status,
    DateTime? StatusChangedAt,
    IReadOnlyList<ChannelParticipantDto> Participants);

/// <summary>Ticket details as seen by the signed-in user (only the channels they take part in).</summary>
public record TicketDetailDto(
    string Number,
    string Title,
    TicketType Type,
    TicketPriority Priority,
    UserRefDto? CreatedBy,
    DateTime CreatedAt,
    IReadOnlyList<TicketChannelDetailDto> Channels);

/// <summary>
/// Channel with its conversation: messages and activity events (both oldest first, merged by
/// the client into one feed), and the files attached to it.
/// </summary>
public record TicketChannelDetailDto(
    long Id,
    TicketStatus Status,
    DateTime? StatusChangedAt,
    IReadOnlyList<ChannelParticipantDto> Participants,
    IReadOnlyList<TicketMessageDto> Messages,
    IReadOnlyList<TicketActivityDto> Activities,
    IReadOnlyList<ChannelFileDto> Files);

/// <summary>File attached to a channel (<see cref="FileId"/> = the stored file, shared by forwards).</summary>
public record ChannelFileDto(
    long FileId,
    string FileName,
    string ContentType,
    long Size,
    UserRefDto AddedBy,
    DateTime AddedAt,
    bool IsForwarded);

/// <summary>File reference shown in an activity event.</summary>
public record FileRefDto(long FileId, string FileName, string ContentType, long Size);

/// <summary>Activity event of a channel: who did what and when (fields depend on <see cref="Type"/>).</summary>
public record TicketActivityDto(
    long Id,
    TicketActivityType Type,
    UserRefDto Actor,
    DateTime CreatedAt,
    TicketStatus? FromStatus,
    TicketStatus? ToStatus,
    TicketType? FromType,
    TicketType? ToType,
    TicketPriority? FromPriority,
    TicketPriority? ToPriority,
    FileRefDto? File);

/// <summary>
/// Forwards a file of the ticket (only its id is linked, the content is not copied) to an
/// existing channel (<see cref="TargetChannelId"/>) or to the channel between
/// <see cref="SourceCustomerId"/> and <see cref="TargetCustomerId"/> (reused or created).
/// </summary>
public record ForwardFileRequest(long? TargetChannelId, long? SourceCustomerId, long? TargetCustomerId);

/// <summary>Result of a file forward: the channel it landed in, and whether that channel was created.</summary>
public record ForwardFileResultDto(long ChannelId, bool ChannelCreated);

/// <summary>
/// Message of a channel. <see cref="AuthorCustomerName"/> = the author's customer in this
/// channel; <see cref="IsMine"/> = written by the signed-in user; <see cref="IsOwnSide"/> =
/// written by someone of the user's own customer(s).
/// </summary>
public record TicketMessageDto(
    long Id,
    long ChannelId,
    UserRefDto Author,
    string? AuthorCustomerName,
    bool IsMine,
    bool IsOwnSide,
    string Content,
    DateTime CreatedAt,
    ForwardedMessageDto? Forwarded);

/// <summary>
/// Original of a forwarded message: author, their customer in the original channel, date
/// and content. <see cref="Content"/> of the forward itself is the forwarder's comment.
/// </summary>
public record ForwardedMessageDto(
    long Id,
    UserRefDto Author,
    string? AuthorCustomerName,
    string Content,
    DateTime CreatedAt);

/// <summary>
/// Forwards a message of the ticket either to an existing channel of the user
/// (<see cref="TargetChannelId"/>), or to the channel between <see cref="SourceCustomerId"/>
/// (a customer of the user) and <see cref="TargetCustomerId"/> — reused if it exists, created
/// otherwise (edge required). <see cref="Comment"/> (HTML) is optional.
/// </summary>
public record ForwardMessageRequest(
    long? TargetChannelId,
    long? SourceCustomerId,
    long? TargetCustomerId,
    [MaxLength(50_000)] string? Comment);

/// <summary>Result of a forward: the channel it landed in, and whether that channel was created.</summary>
public record ForwardResultDto(long ChannelId, bool ChannelCreated, TicketMessageDto Message);

/// <summary>Reply posted in a channel (HTML from the rich text editor).</summary>
public record PostMessageRequest([Required, MaxLength(50_000)] string Content);

/// <summary>New channel on an existing ticket: source (user's customer) → target, with a first message.</summary>
public record OpenChannelRequest(
    [Range(1, long.MaxValue)] long SourceCustomerId,
    [Range(1, long.MaxValue)] long TargetCustomerId,
    [Required, MaxLength(50_000)] string Content);

/// <summary>Status change of one channel of a ticket.</summary>
public record UpdateChannelStatusRequest([Required] TicketStatus? Status);

/// <summary>Channel participant; <see cref="IsOwn"/> = the signed-in user is a member of this customer.</summary>
public record ChannelParticipantDto(long CustomerId, string CustomerName, bool IsOwn);

/// <summary>Minimal customer reference (id + name).</summary>
public record CustomerRefDto(long Id, string Name);

/// <summary>
/// A customer the signed-in user belongs to, with the customers it may send
/// tickets to (active ticket edges).
/// </summary>
public record TicketSendOptionDto(long CustomerId, string CustomerName, IReadOnlyList<CustomerRefDto> Targets);

/// <summary>
/// Ticket creation: sent on behalf of <see cref="SourceCustomerId"/> (a customer of the
/// signed-in user) to <see cref="TargetCustomerId"/>. <see cref="Content"/> (HTML) becomes
/// the first message of the created channel.
/// </summary>
public record CreateTicketRequest(
    [Range(1, long.MaxValue)] long SourceCustomerId,
    [Range(1, long.MaxValue)] long TargetCustomerId,
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(50_000)] string Content,
    TicketType Type = TicketType.REQUEST,
    TicketPriority Priority = TicketPriority.MEDIUM);
