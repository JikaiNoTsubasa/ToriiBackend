using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// Ticket details and conversations: what a user sees of a ticket depends on their
/// customers — only the channels one of them takes part in, with their messages.
/// </summary>
public partial class TicketManager
{
    /// <summary>A channel is visible to the user when one of their (live) customers takes part in it.</summary>
    private static Expression<Func<TicketChannel, bool>> VisibleChannel(long userId) => ch =>
        !ch.IsDeleted && ch.Participants.Any(p =>
            !p.Customer.IsDeleted && p.Customer.Members.Any(u => u.Id == userId));

    /// <summary>
    /// Ticket details for <paramref name="userId"/>: header + the user's channels, each with
    /// its participants and messages (oldest first). 404 when the user sees no channel.
    /// </summary>
    public async Task<TicketDetailDto> GetDetailAsync(string number, long userId, CancellationToken ct = default)
    {
        var header = await QueryVisibleTo(userId)
                         .AsNoTracking()
                         .Where(t => t.Number == number)
                         .Select(t => new
                         {
                             t.Number,
                             t.Title,
                             CreatedBy = t.CreatedBy == null
                                 ? null
                                 : new UserRefDto(t.CreatedBy.Id, t.CreatedBy.DisplayName, t.CreatedBy.AvatarUrl),
                             t.CreatedAt,
                         })
                         .FirstOrDefaultAsync(ct)
                     ?? throw NotFoundException.For("Ticket", number);

        var channels = await _dbContext.TicketChannels
            .AsNoTracking()
            .Where(ch => ch.TicketNumber == number)
            .Where(VisibleChannel(userId))
            .OrderBy(ch => ch.Id)
            .Select(ch => new
            {
                ch.Id,
                ch.Status,
                ch.StatusChangedAt,
                Participants = ch.Participants
                    .OrderBy(p => p.Customer.Name)
                    .Select(p => new ChannelParticipantDto(
                        p.CustomerId, p.Customer.Name, p.Customer.Members.Any(u => u.Id == userId)))
                    .ToList(),
            })
            .ToListAsync(ct);

        var channelIds = channels.Select(ch => ch.Id).ToList();
        var messages = await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(m => channelIds.Contains(m.TicketChannelId) && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(ToMessage(userId))
            .ToListAsync(ct);
        var messagesByChannel = messages.ToLookup(m => m.ChannelId);

        return new TicketDetailDto(
            header.Number,
            header.Title,
            header.CreatedBy,
            header.CreatedAt,
            channels
                .Select(ch => new TicketChannelDetailDto(
                    ch.Id, ch.Status, ch.StatusChangedAt, ch.Participants, messagesByChannel[ch.Id].ToList()))
                .ToList());
    }

    /// <summary>
    /// Posts a reply in one channel of the ticket. Only members of a customer of that
    /// channel can post; otherwise the channel is reported as not found (no leak).
    /// </summary>
    public async Task<TicketMessageDto> PostMessageAsync(
        string number, long channelId, PostMessageRequest request, long userId, CancellationToken ct = default)
    {
        if (IsBlankHtml(request.Content))
            throw new ValidationException(new Dictionary<string, string[]> { ["content"] = ["Content is required."] });

        var channel = await _dbContext.TicketChannels
                          .Where(ch => ch.Id == channelId && ch.TicketNumber == number)
                          .Where(VisibleChannel(userId))
                          .FirstOrDefaultAsync(ct)
                      ?? throw NotFoundException.For("Ticket channel", $"{number}/{channelId}");

        var now = DateTime.UtcNow;
        var message = new TicketMessage
        {
            TicketChannelId = channel.Id,
            AuthorId = userId,
            Content = request.Content,
            CreatedAt = now,
            CreatedById = userId,
            UpdatedAt = null,
        };
        _dbContext.TicketMessages.Add(message);
        channel.UpdatedAt = now;
        channel.UpdatedById = userId;
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(m => m.Id == message.Id)
            .Select(ToMessage(userId))
            .FirstAsync(ct);
    }

    /// <summary>
    /// Opens a new channel on an existing ticket, from one of the user's customers (who must
    /// already see the ticket) to a customer allowed by an active ticket edge, with a first
    /// message. 409 if a live channel already links these two customers on this ticket.
    /// </summary>
    public async Task<TicketDetailDto> OpenChannelAsync(
        string number, OpenChannelRequest request, long userId, CancellationToken ct = default)
    {
        if (IsBlankHtml(request.Content))
            throw new ValidationException(new Dictionary<string, string[]> { ["content"] = ["Content is required."] });

        if (!await QueryVisibleTo(userId).AnyAsync(t => t.Number == number, ct))
            throw NotFoundException.For("Ticket", number);

        await EnsureCanSendAsync(request.SourceCustomerId, request.TargetCustomerId, userId, ct);

        var alreadyLinked = await _dbContext.TicketChannels.AnyAsync(ch =>
            ch.TicketNumber == number && !ch.IsDeleted &&
            ch.Participants.Any(p => p.CustomerId == request.SourceCustomerId) &&
            ch.Participants.Any(p => p.CustomerId == request.TargetCustomerId), ct);
        if (alreadyLinked)
            throw new ConflictException("A channel already links these two customers on this ticket.");

        var channel = NewChannel(request.SourceCustomerId, request.TargetCustomerId, request.Content, userId);
        channel.TicketNumber = number;
        _dbContext.TicketChannels.Add(channel);
        await _dbContext.SaveChangesAsync(ct);

        return await GetDetailAsync(number, userId, ct);
    }

    /// <summary>The user is a member of the source customer and an active edge allows source → target.</summary>
    private async Task EnsureCanSendAsync(long sourceCustomerId, long targetCustomerId, long userId, CancellationToken ct)
    {
        await EnsureMemberAsync(sourceCustomerId, userId, ct);

        if (!await edges.CanSendAsync(sourceCustomerId, targetCustomerId, ct))
            throw new ForbiddenException("The source customer is not allowed to send tickets to this customer.");
    }

    /// <summary>The user is a member of the (live) customer.</summary>
    private async Task EnsureMemberAsync(long customerId, long userId, CancellationToken ct)
    {
        var isMember = await _dbContext.Customers.AnyAsync(c =>
            c.Id == customerId && !c.IsDeleted && c.Members.Any(u => u.Id == userId), ct);
        if (!isMember)
            throw new ForbiddenException("You are not a member of the source customer.");
    }

    /// <summary>New channel between two customers, with the first message authored by the user.</summary>
    private static TicketChannel NewChannel(long sourceCustomerId, long targetCustomerId, string content, long userId) => new()
    {
        CreatedById = userId,
        UpdatedAt = null,
        Participants =
        [
            new ChannelParticipant { CustomerId = sourceCustomerId },
            new ChannelParticipant { CustomerId = targetCustomerId },
        ],
        Messages =
        [
            new TicketMessage
            {
                AuthorId = userId,
                Content = content,
                CreatedById = userId,
                UpdatedAt = null,
            },
        ],
    };

    /// <summary>
    /// Message projection for <paramref name="userId"/>: the author's customer in the channel,
    /// and whether the author is on the user's side (one of the user's own customers).
    /// </summary>
    private static Expression<Func<TicketMessage, TicketMessageDto>> ToMessage(long userId) => m => new TicketMessageDto(
        m.Id,
        m.TicketChannelId,
        new UserRefDto(m.Author.Id, m.Author.DisplayName, m.Author.AvatarUrl),
        m.TicketChannel.Participants
            .Where(p => p.Customer.Members.Any(u => u.Id == m.AuthorId))
            .Select(p => p.Customer.Name)
            .FirstOrDefault(),
        m.AuthorId == userId,
        m.TicketChannel.Participants.Any(p =>
            p.Customer.Members.Any(u => u.Id == userId) && p.Customer.Members.Any(u => u.Id == m.AuthorId)),
        m.Content,
        m.CreatedAt,
        m.ForwardedFrom == null
            ? null
            : new ForwardedMessageDto(
                m.ForwardedFrom.Id,
                new UserRefDto(m.ForwardedFrom.Author.Id, m.ForwardedFrom.Author.DisplayName, m.ForwardedFrom.Author.AvatarUrl),
                m.ForwardedFrom.TicketChannel.Participants
                    .Where(p => p.Customer.Members.Any(u => u.Id == m.ForwardedFrom.AuthorId))
                    .Select(p => p.Customer.Name)
                    .FirstOrDefault(),
                m.ForwardedFrom.Content,
                m.ForwardedFrom.CreatedAt));
}
