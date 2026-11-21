using Microsoft.EntityFrameworkCore;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>Forwarding a message of a ticket to another channel of the same ticket.</summary>
public partial class TicketManager
{
    /// <summary>
    /// Forwards message <paramref name="messageId"/> (in a channel the user sees) to another
    /// channel of the ticket: an existing channel of the user (<c>TargetChannelId</c>), or the
    /// channel between <c>SourceCustomerId</c> (a customer of the user) and <c>TargetCustomerId</c>,
    /// reused if it exists and created otherwise (an active edge is then required). The forward
    /// is a new message authored by the user, holding the optional comment and a link to the
    /// root original (a forward without comment is never chained).
    /// </summary>
    public async Task<ForwardResultDto> ForwardMessageAsync(
        string number, long channelId, long messageId, ForwardMessageRequest request, long userId,
        CancellationToken ct = default)
    {
        var hasChannel = request.TargetChannelId is not null;
        var hasPair = request.SourceCustomerId is not null && request.TargetCustomerId is not null;
        if (hasChannel == hasPair)
            throw new ValidationException("Choose either an existing channel or a source and a target customer.");

        var sourceVisible = await _dbContext.TicketChannels
            .Where(ch => ch.Id == channelId && ch.TicketNumber == number)
            .Where(VisibleChannel(userId))
            .AnyAsync(ct);
        if (!sourceVisible)
            throw NotFoundException.For("Ticket channel", $"{number}/{channelId}");

        var original = await _dbContext.TicketMessages
                           .AsNoTracking()
                           .Where(m => m.Id == messageId && m.TicketChannelId == channelId && !m.IsDeleted)
                           .Select(m => new { m.Id, m.ForwardedFromId, m.Content })
                           .FirstOrDefaultAsync(ct)
                       ?? throw NotFoundException.For("Message", messageId);
        var rootId = original.ForwardedFromId is { } forwardedFrom && IsBlankHtml(original.Content)
            ? forwardedFrom
            : original.Id;

        var (target, created) = hasChannel
            ? (await FindVisibleChannelAsync(number, request.TargetChannelId!.Value, userId, ct), false)
            : await FindOrCreateChannelAsync(number, request.SourceCustomerId!.Value, request.TargetCustomerId!.Value, userId, ct);

        if (!created && target.Id == channelId)
            throw new ValidationException("The message is already in this channel: pick another one.");

        var now = DateTime.UtcNow;
        var forward = new TicketMessage
        {
            TicketChannel = target,
            AuthorId = userId,
            Content = request.Comment is { } comment && !IsBlankHtml(comment) ? comment : "",
            ForwardedFromId = rootId,
            CreatedAt = now,
            CreatedById = userId,
            UpdatedAt = null,
        };
        _dbContext.TicketMessages.Add(forward);
        if (!created)
        {
            target.UpdatedAt = now;
            target.UpdatedById = userId;
        }
        await _dbContext.SaveChangesAsync(ct);

        var dto = await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(m => m.Id == forward.Id)
            .Select(ToMessage(userId))
            .FirstAsync(ct);
        return new ForwardResultDto(target.Id, created, dto);
    }

    private async Task<TicketChannel> FindVisibleChannelAsync(string number, long channelId, long userId, CancellationToken ct)
        => await _dbContext.TicketChannels
               .Where(ch => ch.Id == channelId && ch.TicketNumber == number)
               .Where(VisibleChannel(userId))
               .FirstOrDefaultAsync(ct)
           ?? throw NotFoundException.For("Ticket channel", $"{number}/{channelId}");

    /// <summary>
    /// Live channel of the ticket between the two customers, or a new one. Reusing an existing
    /// channel needs no edge (like replying); creating one does.
    /// </summary>
    private async Task<(TicketChannel Channel, bool Created)> FindOrCreateChannelAsync(
        string number, long sourceCustomerId, long targetCustomerId, long userId, CancellationToken ct)
    {
        await EnsureMemberAsync(sourceCustomerId, userId, ct);

        var existing = await _dbContext.TicketChannels.FirstOrDefaultAsync(ch =>
            ch.TicketNumber == number && !ch.IsDeleted &&
            ch.Participants.Any(p => p.CustomerId == sourceCustomerId) &&
            ch.Participants.Any(p => p.CustomerId == targetCustomerId), ct);
        if (existing is not null)
            return (existing, false);

        await EnsureCanSendAsync(sourceCustomerId, targetCustomerId, userId, ct);
        var channel = NewEmptyChannel(sourceCustomerId, targetCustomerId, userId);
        channel.TicketNumber = number;
        _dbContext.TicketChannels.Add(channel);
        return (channel, true);
    }
}
