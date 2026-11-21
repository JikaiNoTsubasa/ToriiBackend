namespace Torii.Api.Models;

/// <summary>
/// Status of a <see cref="TicketChannel"/> (each channel of a ticket has its own).
/// Stored and serialized as the UPPERCASE name.
/// </summary>
public enum TicketStatus
{
    /// <summary>Created, not yet looked at (default).</summary>
    NEW,

    /// <summary>Seen and accepted, work not started yet.</summary>
    ACKNOWLEDGED,

    /// <summary>Being worked on.</summary>
    IN_PROGRESS,

    /// <summary>Waiting for an answer from the requester.</summary>
    WAITING_FOR_REQUESTER,

    /// <summary>Waiting for an external party (supplier, other team…).</summary>
    WAITING_FOR_THIRD_PARTY,

    /// <summary>Deliberately paused.</summary>
    ON_HOLD,

    /// <summary>A solution was provided, waiting for confirmation.</summary>
    RESOLVED,

    /// <summary>Done.</summary>
    CLOSED,

    /// <summary>Dropped without resolution.</summary>
    CANCELLED,
}
