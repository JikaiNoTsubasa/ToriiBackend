namespace Torii.Api.Exceptions;

/// <summary>Authenticated, but not allowed to perform this operation (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public override string Title => "Forbidden";
}
