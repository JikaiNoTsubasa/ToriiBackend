using System.ComponentModel.DataAnnotations;
using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>Ticket as listed for the signed-in user (only the channels they take part in).</summary>
public record TicketListItemDto(
    string Number,
    string Title,
    UserRefDto? CreatedBy,
    DateTime CreatedAt,
    IReadOnlyList<TicketChannelDto> Channels);

/// <summary>Minimal user reference for display (name + avatar).</summary>
public record UserRefDto(long Id, string DisplayName, string AvatarUrl);

public record TicketChannelDto(
    long Id,
    TicketStatus Status,
    DateTime? StatusChangedAt,
    IReadOnlyList<ChannelParticipantDto> Participants);

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
    [Required, MaxLength(50_000)] string Content);
