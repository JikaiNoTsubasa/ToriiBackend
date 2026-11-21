namespace Torii.Api.Exceptions;

/// <summary>Ressource demandée inexistante (HTTP 404).</summary>
public sealed class NotFoundException(string message) : ToriiException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Ressource introuvable";

    /// <summary>Fabrique un message standard « {ressource} #{clé} introuvable ».</summary>
    public static NotFoundException For(string resource, object key) =>
        new($"{resource} avec l'identifiant « {key} » est introuvable.");
}
