namespace Torii.Api.Dtos;

/// <summary>Ticket as listed for the signed-in user.</summary>
public record TicketListItemDto(string Number, IReadOnlyList<TicketChannelDto> Channels);

public record TicketChannelDto(long Id, IReadOnlyList<ChannelParticipantDto> Participants);

public record ChannelParticipantDto(long CustomerId, string CustomerName);
