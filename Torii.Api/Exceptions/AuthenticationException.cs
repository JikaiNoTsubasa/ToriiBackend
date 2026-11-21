namespace Torii.Api.Exceptions;

/// <summary>Authentication failure: invalid credentials or token (HTTP 401).</summary>
public sealed class AuthenticationException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public override string Title => "Authentication failed";
}
