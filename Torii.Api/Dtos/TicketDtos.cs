using System.ComponentModel.DataAnnotations;

namespace Torii.Api.Dtos;

/// <summary>Ticket as listed for the signed-in user.</summary>
public record TicketListItemDto(string Number, string Title, IReadOnlyList<TicketChannelDto> Channels);

public record TicketChannelDto(long Id, IReadOnlyList<ChannelParticipantDto> Participants);

public record ChannelParticipantDto(long CustomerId, string CustomerName);

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
