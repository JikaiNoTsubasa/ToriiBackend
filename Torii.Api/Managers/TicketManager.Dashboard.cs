using Microsoft.EntityFrameworkCore;
using Torii.Api.Dtos;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>Home dashboard: KPIs, breakdowns, activity and lists over the user's channels.</summary>
public partial class TicketManager
{
    private const int ActivityDays = 14;
    private const int DashboardListSize = 6;

    private static readonly TicketStatus[] FinalStatuses = [TicketStatus.RESOLVED, TicketStatus.CLOSED, TicketStatus.CANCELLED];
    private static readonly TicketStatus[] ResolvedStatuses = [TicketStatus.RESOLVED, TicketStatus.CLOSED];

    public async Task<DashboardDto> GetDashboardAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var visibleChannels = _dbContext.TicketChannels.Where(VisibleChannel(userId));

        // Open channels with their latest message: drives the open / awaiting KPIs, the
        // "needs attention" list and the priority breakdown (one row per channel, small).
        var open = await visibleChannels
            .AsNoTracking()
            .Where(ch => !FinalStatuses.Contains(ch.Status))
            .Select(ch => new
            {
                ch.Id,
                ch.TicketNumber,
                ch.Ticket.Title,
                ch.Ticket.Type,
                ch.Ticket.Priority,
                ch.Status,
                Counterparts = ch.Participants
                    .Where(p => !p.Customer.Members.Any(u => u.Id == userId))
                    .Select(p => p.Customer.Name)
                    .ToList(),
                Last = ch.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                    .Select(m => new
                    {
                        m.CreatedAt,
                        Author = new UserRefDto(m.Author.Id, m.Author.DisplayName, m.Author.AvatarUrl),
                        // Same rule as TicketMessageDto.IsOwnSide.
                        OwnSide = ch.Participants.Any(p =>
                            p.Customer.Members.Any(u => u.Id == userId) && p.Customer.Members.Any(u => u.Id == m.AuthorId)),
                    })
                    .FirstOrDefault(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        var awaiting = open.Where(ch => ch.Last is { OwnSide: false }).ToList();
        var needsAttention = awaiting
            .OrderBy(ch => ch.Priority)              // enum order: CRITICAL first
            .ThenBy(ch => ch.Last!.CreatedAt)        // longest waiting first
            .Take(DashboardListSize)
            .Select(ch => new AttentionItemDto(
                ch.TicketNumber, ch.Title, ch.Type, ch.Priority, ch.Id, ch.Status,
                string.Join(", ", ch.Counterparts.Order()), ch.Last!.Author, ch.Last.CreatedAt))
            .ToList();

        var openTicketsByPriority = open
            .DistinctBy(ch => ch.TicketNumber)
            .GroupBy(ch => ch.Priority)
            .Select(g => new PriorityCountDto(g.Key, g.Count()))
            .OrderBy(p => p.Priority)
            .ToList();

        var byStatus = await visibleChannels
            .GroupBy(ch => ch.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var channelsByStatus = Enum.GetValues<TicketStatus>()
            .Select(s => new StatusCountDto(s, byStatus.FirstOrDefault(b => b.Status == s)?.Count ?? 0))
            .ToList();

        var resolvedSince = now.AddDays(-30);
        var resolved = await visibleChannels.CountAsync(ch =>
            ResolvedStatuses.Contains(ch.Status) && ch.StatusChangedAt >= resolvedSince, ct);

        var createdSince = now.AddDays(-7);
        var created = await QueryVisibleTo(userId).CountAsync(t => t.CreatedAt >= createdSince, ct);

        // Messages per UTC day, every day of the range present (zeros included).
        var firstDay = DateOnly.FromDateTime(now).AddDays(1 - ActivityDays);
        var activitySince = firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var visibleIds = visibleChannels.Select(ch => ch.Id);
        var perDay = await _dbContext.TicketMessages
            .Where(m => !m.IsDeleted && m.CreatedAt >= activitySince && visibleIds.Contains(m.TicketChannelId))
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var messagesPerDay = Enumerable.Range(0, ActivityDays)
            .Select(i => firstDay.AddDays(i))
            .Select(day => new DailyCountDto(
                day, perDay.FirstOrDefault(d => DateOnly.FromDateTime(d.Day) == day)?.Count ?? 0))
            .ToList();

        var latestTickets = await QueryVisibleTo(userId)
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number)
            .Take(DashboardListSize)
            .Select(ToListItem(userId))
            .AsSplitQuery()
            .ToListAsync(ct);

        return new DashboardDto(
            new DashboardKpisDto(open.Count, awaiting.Count, created, resolved, messagesPerDay.Sum(d => d.Count)),
            channelsByStatus,
            openTicketsByPriority,
            messagesPerDay,
            needsAttention,
            latestTickets);
    }
}
