namespace Torii.Api.Exceptions;

/// <summary>Invalid request data (HTTP 400), with optional per-field details.</summary>
public sealed class ValidationException : ToriiException
{
    private const string DefaultMessage = "One or more validation errors occurred.";

    public ValidationException(string message) : base(message) { }

    public ValidationException(
        IReadOnlyDictionary<string, string[]> errors, string message = DefaultMessage)
        : base(message) => Errors = errors;

    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string Title => "Invalid request";
    public override IReadOnlyDictionary<string, string[]>? Errors { get; }
}
