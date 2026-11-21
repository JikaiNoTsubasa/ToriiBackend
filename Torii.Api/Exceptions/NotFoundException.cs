namespace Torii.Api.Exceptions;

/// <summary>Requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Resource not found";

    /// <summary>Builds a standard "{resource} with id '{key}' was not found" message.</summary>
    public static NotFoundException For(string resource, object key) =>
        new($"{resource} with id '{key}' was not found.");
}
