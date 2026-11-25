using Torii.Api.Models;

namespace Torii.Api.Dtos;

/// <summary>
/// In-app notification of the signed-in user. <see cref="Counterpart"/> = the other
/// customer(s) of the channel; <see cref="Preview"/> = plain-text excerpt of the latest
/// message (its comment, or the forwarded content); <see cref="Count"/> = messages covered.
/// For ACCOUNT_REQUESTED: the ticket fields are null, <see cref="Actor"/> is the requester and
/// <see cref="Preview"/> their email. <see cref="IsInternal"/>: posted in the internal thread of
/// the recipient's customer (no counterpart).
/// </summary>
public record NotificationDto(
    long Id,
    NotificationType Type,
    string? TicketNumber,
    string? TicketTitle,
    long? ChannelId,
    string Counterpart,
    UserRefDto Actor,
    string Preview,
    bool IsForward,
    bool IsInternal,
    int Count,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>Latest notifications (newest first) and the total number of unread ones.</summary>
public record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

/// <summary>
/// Unread total and the time of the latest unread notification (null when none): a later
/// <see cref="LatestAt"/> than last seen means something new arrived, even when the count did
/// not change (a coalesced notification bumped by a new message).
/// </summary>
public record UnreadSummaryDto(int Count, DateTime? LatestAt);
