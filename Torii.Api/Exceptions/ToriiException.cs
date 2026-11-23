namespace Torii.Api.Exceptions;

/// <summary>
/// Base of the API business exceptions. Each exception carries the HTTP status code and
/// the title used to build the <c>ProblemDetails</c> response (RFC 9457).
/// Any exception that does not inherit from this class is treated as an unexpected
/// error (500) by <c>GlobalExceptionHandler</c>.
/// </summary>
public abstract class ToriiException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>HTTP status code returned to the client.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Short, stable error title, independent of the message.</summary>
    public abstract string Title { get; }

    /// <summary>Optional validation errors (field name → messages).</summary>
    public virtual IReadOnlyDictionary<string, string[]>? Errors => null;

    /// <summary>Optional machine-readable code, for clients reacting to a specific case.</summary>
    public virtual string? Code => null;
}
