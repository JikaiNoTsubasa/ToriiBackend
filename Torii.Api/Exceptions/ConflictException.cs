namespace Torii.Api.Exceptions;

/// <summary>Conflit avec l'état courant de la ressource, ex. doublon (HTTP 409).</summary>
public sealed class ConflictException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflit";
}
