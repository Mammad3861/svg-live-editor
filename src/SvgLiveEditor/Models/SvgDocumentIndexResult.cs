namespace SvgLiveEditor.Models;

public sealed record SvgDocumentIndexResult(
    SvgValidationResult Validation,
    SvgDocumentIndex? Document,
    string? IndexError)
{
    public bool IsIndexed => Validation.IsValid && Document is not null;
}
