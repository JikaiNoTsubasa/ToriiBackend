namespace Torii.Api.Exceptions;

/// <summary>Conflict with the current state of the resource, e.g. a duplicate (HTTP 409).</summary>
public sealed class ConflictException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflict";
}
