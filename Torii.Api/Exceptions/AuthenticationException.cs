namespace Torii.Api.Exceptions;

/// <summary>Échec d'authentification : identifiants ou jeton invalides (HTTP 401).</summary>
public sealed class AuthenticationException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public override string Title => "Échec de l'authentification";
}
