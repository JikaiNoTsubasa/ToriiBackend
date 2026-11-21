using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Models;

namespace Torii.Api.Managers;

public class TicketManager(ToriiDbContext db) : ToriManager(db)
{
    /// <summary>
    /// Visibility rule, to reuse for every ticket access (list, details,
    /// update…): a ticket is visible if the user is a member of a customer
    /// taking part in one of its channels. Deleted (soft delete) channels and
    /// customers do not grant access. No exception for ADMIN.
    /// </summary>
    public IQueryable<Ticket> QueryVisibleTo(long userId) =>
        _dbContext.Tickets.Where(t => t.Channels!.Any(ch =>
            !ch.IsDeleted &&
            ch.Participants.Any(p =>
                !p.Customer.IsDeleted &&
                p.Customer.Members.Any(u => u.Id == userId))));

    public async Task<IReadOnlyList<TicketListItemDto>> ListVisibleAsync(long userId, CancellationToken ct = default)
        => await QueryVisibleTo(userId)
            .AsNoTracking()
            .OrderBy(t => t.Number)
            .Select(t => new TicketListItemDto(
                t.Number,
                t.Channels!
                    .Where(ch => !ch.IsDeleted)
                    .OrderBy(ch => ch.Id)
                    .Select(ch => new TicketChannelDto(
                        ch.Id,
                        ch.Participants
                            .OrderBy(p => p.Customer.Name)
                            .Select(p => new ChannelParticipantDto(p.CustomerId, p.Customer.Name))
                            .ToList()))
                    .ToList()))
            .AsSplitQuery()
            .ToListAsync(ct);
}
