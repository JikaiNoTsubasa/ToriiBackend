namespace Torii.Api.Exceptions;

/// <summary>
/// Singpass sign-in could not be completed: unavailable, expired attempt, or an error from
/// Singpass itself (HTTP 502). The message is safe to show; details are logged server-side.
/// </summary>
public sealed class SingpassException(string message, int statusCode = StatusCodes.Status502BadGateway)
    : ToriiException(message)
{
    public override int StatusCode { get; } = statusCode;
    public override string Title => "Singpass sign-in failed";
}
