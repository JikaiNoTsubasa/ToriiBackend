namespace Torii.Api.Exceptions;

/// <summary>Données de requête invalides (HTTP 400), avec détail par champ optionnel.</summary>
public sealed class ValidationException : ToriiException
{
    private const string DefaultMessage = "Une ou plusieurs erreurs de validation sont survenues.";

    public ValidationException(string message) : base(message) { }

    public ValidationException(
        IReadOnlyDictionary<string, string[]> errors, string message = DefaultMessage)
        : base(message) => Errors = errors;

    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string Title => "Requête invalide";
    public override IReadOnlyDictionary<string, string[]>? Errors { get; }
}
