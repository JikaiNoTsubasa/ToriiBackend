namespace Torii.Api.Exceptions;

/// <summary>
/// Base des exceptions métier de l'API. Chaque exception porte le code HTTP et
/// le titre utilisés pour construire la réponse <c>ProblemDetails</c> (RFC 9457).
/// Toute exception qui n'hérite pas de cette classe est traitée comme une erreur
/// inattendue (500) par <c>GlobalExceptionHandler</c>.
/// </summary>
public abstract class ToriiException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Code de statut HTTP renvoyé au client.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Titre court et stable de l'erreur, indépendant du message.</summary>
    public abstract string Title { get; }

    /// <summary>Erreurs de validation optionnelles (nom du champ → messages).</summary>
    public virtual IReadOnlyDictionary<string, string[]>? Errors => null;
}
