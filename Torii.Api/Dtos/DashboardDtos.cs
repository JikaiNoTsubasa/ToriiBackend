using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>
/// Home dashboard of the signed-in user, computed on the channels they can see (same rule as
/// the ticket list). Days are UTC days.
/// </summary>
public record DashboardDto(
    DashboardKpisDto Kpis,
    IReadOnlyList<StatusCountDto> ChannelsByStatus,
    IReadOnlyList<PriorityCountDto> OpenTicketsByPriority,
    IReadOnlyList<DailyCountDto> MessagesPerDay,
    IReadOnlyList<AttentionItemDto> NeedsAttention,
    IReadOnlyList<TicketListItemDto> LatestTickets);

/// <param name="OpenChannels">Channels in a non-final status (not resolved / closed / cancelled).</param>
/// <param name="AwaitingReply">Open channels whose latest message comes from the other side.</param>
/// <param name="CreatedLast7Days">Tickets created in the last 7 days.</param>
/// <param name="ResolvedLast30Days">Channels moved to RESOLVED or CLOSED in the last 30 days.</param>
/// <param name="MessagesLast14Days">Messages posted in the user's channels over the last 14 days.</param>
public record DashboardKpisDto(
    int OpenChannels,
    int AwaitingReply,
    int CreatedLast7Days,
    int ResolvedLast30Days,
    int MessagesLast14Days);

public record StatusCountDto(TicketStatus Status, int Count);

public record PriorityCountDto(TicketPriority Priority, int Count);

/// <summary>Count of one UTC day (every day of the range is present, zeros included).</summary>
public record DailyCountDto(DateOnly Date, int Count);

/// <summary>Open channel waiting for the user's side to answer (latest message from the other side).</summary>
public record AttentionItemDto(
    string TicketNumber,
    string Title,
    TicketType Type,
    TicketPriority Priority,
    long ChannelId,
    TicketStatus Status,
    string Counterpart,
    UserRefDto LastAuthor,
    DateTime LastMessageAt);
