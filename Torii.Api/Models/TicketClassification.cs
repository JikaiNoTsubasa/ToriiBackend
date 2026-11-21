namespace Torii.Api.Models;

/// <summary>Kind of ticket (ticket-level: shared by all its channels).</summary>
public enum TicketType
{
    /// <summary>Something does not work as expected.</summary>
    BUG,

    /// <summary>A service is disrupted or down.</summary>
    INCIDENT,

    /// <summary>A new capability is wanted.</summary>
    FEATURE_REQUEST,

    /// <summary>An existing capability could be better.</summary>
    IMPROVEMENT,

    /// <summary>A service or an action is asked for (default).</summary>
    REQUEST,

    /// <summary>Information is needed.</summary>
    QUESTION,
}

/// <summary>Urgency of a ticket (ticket-level: shared by all its channels).</summary>
public enum TicketPriority
{
    CRITICAL,
    HIGH,
    MEDIUM,
    LOW,
}
