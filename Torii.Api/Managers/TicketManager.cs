using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Microsoft.Extensions.Options;
using Torii.Api.Exceptions;
using Torii.Api.Models;
using Torii.Api.Services;

namespace Torii.Api.Managers;

public partial class TicketManager(
    ToriiDbContext db,
    TicketEdgeManager edges,
    IFileStorage storage,
    IOptions<FileStorageOptions> storageOptions) : ToriManager(db)
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

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// One page of the tickets visible to the user, newest first, filtered by number / title
    /// and by the status of the user's own channels (see <see cref="TicketListQuery"/>).
    /// </summary>
    public async Task<PagedResult<TicketListItemDto>> ListVisibleAsync(
        long userId, TicketListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var tickets = QueryVisibleTo(userId).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            tickets = tickets.Where(t => EF.Functions.ILike(t.Number, pattern, "\\")
                                      || EF.Functions.ILike(t.Title, pattern, "\\"));
        }

        if (query.Statuses is { } statuses)
        {
            // Only the channels the user takes part in count (the ones they see).
            var wanted = statuses.Distinct().ToList();
            tickets = tickets.Where(t => t.Channels!.Any(ch =>
                !ch.IsDeleted && wanted.Contains(ch.Status) &&
                ch.Participants.Any(p => !p.Customer.IsDeleted && p.Customer.Members.Any(u => u.Id == userId))));
        }

        if (query.Types is { } types)
        {
            var wantedTypes = types.Distinct().ToList();
            tickets = tickets.Where(t => wantedTypes.Contains(t.Type));
        }

        if (query.Priorities is { } priorities)
        {
            var wantedPriorities = priorities.Distinct().ToList();
            tickets = tickets.Where(t => wantedPriorities.Contains(t.Priority));
        }

        var total = await tickets.CountAsync(ct);
        var items = total == 0
            ? []
            : await tickets
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Number)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToListItem(userId))
                .AsSplitQuery()
                .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, page, pageSize, total);
    }

    /// <summary>Escapes LIKE wildcards typed by the user.</summary>
    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// Customers the user belongs to that may send tickets, each with its allowed
    /// targets (active edges). Customers without any target are omitted.
    /// </summary>
    public async Task<IReadOnlyList<TicketSendOptionDto>> GetSendOptionsAsync(long userId, CancellationToken ct = default)
    {
        var rows = await edges.QuerySendable()
            .AsNoTracking()
            .Where(e => e.SourceCustomer.Members.Any(u => u.Id == userId))
            .Select(e => new
            {
                e.SourceCustomerId,
                SourceName = e.SourceCustomer.Name,
                e.TargetCustomerId,
                TargetName = e.TargetCustomer.Name,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => (r.SourceCustomerId, r.SourceName))
            .OrderBy(g => g.Key.SourceName)
            .Select(g => new TicketSendOptionDto(
                g.Key.SourceCustomerId,
                g.Key.SourceName,
                g.OrderBy(r => r.TargetName)
                 .Select(r => new CustomerRefDto(r.TargetCustomerId, r.TargetName))
                 .ToList()))
            .ToList();
    }

    /// <summary>
    /// Creates a ticket from <c>SourceCustomerId</c> to <c>TargetCustomerId</c>, with its
    /// channel (both customers as participants) and a first message holding the content.
    /// The user must be a member of the source customer and an active edge must allow
    /// source → target. Everything is saved in a single transaction.
    /// </summary>
    public async Task<TicketListItemDto> CreateAsync(CreateTicketRequest request, long userId, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var errors = new Dictionary<string, string[]>();
        if (title.Length == 0)
            errors["title"] = ["Title is required."];
        if (IsBlankHtml(request.Content))
            errors["content"] = ["Content is required."];
        if (errors.Count > 0)
            throw new ValidationException(errors);

        await EnsureCanSendAsync(request.SourceCustomerId, request.TargetCustomerId, userId, ct);

        var channel = NewChannel(request.SourceCustomerId, request.TargetCustomerId, request.Content, userId);
        var ticket = new Ticket
        {
            Title = title,
            Type = request.Type,
            Priority = request.Priority,
            CreatedById = userId,
            Channels = [channel],
        };

        _dbContext.Tickets.Add(ticket);
        // The ticket number is generated by PostgreSQL and read back here.
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.Number == ticket.Number)
            .Select(ToListItem(userId))
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    /// <summary>
    /// Changes the status of one channel of a ticket. Allowed to members of any customer
    /// taking part in that channel; otherwise the channel is reported as not found (no leak).
    /// </summary>
    public async Task<TicketListItemDto> UpdateChannelStatusAsync(
        string number, long channelId, TicketStatus status, long userId, CancellationToken ct = default)
    {
        var channel = await _dbContext.TicketChannels.FirstOrDefaultAsync(ch =>
                          ch.Id == channelId && ch.TicketNumber == number && !ch.IsDeleted &&
                          ch.Participants.Any(p =>
                              !p.Customer.IsDeleted && p.Customer.Members.Any(u => u.Id == userId)), ct)
                      ?? throw NotFoundException.For("Ticket channel", $"{number}/{channelId}");

        if (channel.Status != status)
        {
            var now = DateTime.UtcNow;
            _dbContext.TicketActivities.Add(new TicketActivity
            {
                TicketChannelId = channel.Id,
                Type = TicketActivityType.STATUS_CHANGED,
                ActorId = userId,
                CreatedAt = now,
                FromStatus = channel.Status,
                ToStatus = status,
            });
            channel.Status = status;
            channel.StatusChangedAt = now;
            channel.StatusChangedById = userId;
            channel.UpdatedAt = now;
            channel.UpdatedById = userId;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.Number == number)
            .Select(ToListItem(userId))
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    /// <summary>
    /// Changes the type and priority of a ticket the user sees (they are shared by all its
    /// channels). 404 when the user takes part in no channel of the ticket.
    /// </summary>
    public async Task<TicketListItemDto> UpdateClassificationAsync(
        string number, TicketType type, TicketPriority priority, long userId, CancellationToken ct = default)
    {
        var ticket = await QueryVisibleTo(userId).FirstOrDefaultAsync(t => t.Number == number, ct)
                     ?? throw NotFoundException.For("Ticket", number);

        if (ticket.Type != type || ticket.Priority != priority)
        {
            // Type and priority belong to the ticket: the change is logged in the feed of every
            // channel (both sides see it), not only in the channels visible to the user.
            var channelIds = await _dbContext.TicketChannels
                .Where(ch => ch.TicketNumber == number && !ch.IsDeleted)
                .Select(ch => ch.Id)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var channelId in channelIds)
            {
                if (ticket.Type != type)
                {
                    _dbContext.TicketActivities.Add(new TicketActivity
                    {
                        TicketChannelId = channelId,
                        Type = TicketActivityType.TYPE_CHANGED,
                        ActorId = userId,
                        CreatedAt = now,
                        FromType = ticket.Type,
                        ToType = type,
                    });
                }
                if (ticket.Priority != priority)
                {
                    _dbContext.TicketActivities.Add(new TicketActivity
                    {
                        TicketChannelId = channelId,
                        Type = TicketActivityType.PRIORITY_CHANGED,
                        ActorId = userId,
                        CreatedAt = now,
                        FromPriority = ticket.Priority,
                        ToPriority = priority,
                    });
                }
            }

            ticket.Type = type;
            ticket.Priority = priority;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.Number == number)
            .Select(ToListItem(userId))
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    /// <summary>
    /// List projection for <paramref name="userId"/>: only the channels the user takes part in
    /// (each with its own status), and each participant tells whether it is one of the user's
    /// own customers (<c>IsOwn</c>), so the UI can show the counterpart.
    /// </summary>
    private static Expression<Func<Ticket, TicketListItemDto>> ToListItem(long userId) => t => new TicketListItemDto(
        t.Number,
        t.Title,
        t.Type,
        t.Priority,
        t.CreatedBy == null ? null : new UserRefDto(t.CreatedBy.Id, t.CreatedBy.DisplayName, t.CreatedBy.AvatarUrl),
        t.CreatedAt,
        t.Channels!
            .Where(ch => !ch.IsDeleted && ch.Participants.Any(p =>
                !p.Customer.IsDeleted && p.Customer.Members.Any(u => u.Id == userId)))
            .OrderBy(ch => ch.Id)
            .Select(ch => new TicketChannelDto(
                ch.Id,
                ch.Status,
                ch.StatusChangedAt,
                ch.Participants
                    .OrderBy(p => p.Customer.Name)
                    .Select(p => new ChannelParticipantDto(
                        p.CustomerId,
                        p.Customer.Name,
                        p.Customer.Members.Any(u => u.Id == userId)))
                    .ToList()))
            .ToList());

    /// <summary>True when the HTML holds no visible text (e.g. "&lt;p&gt;&lt;/p&gt;").</summary>
    private static bool IsBlankHtml(string html)
        => string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(HtmlTag().Replace(html, " ")));

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();
}
