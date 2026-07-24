namespace SvgLiveEditor.Models;

public sealed record SvgValidationResult(
    bool IsValid,
    string Message,
    int? LineNumber = null,
    int? ColumnNumber = null)
{
    public static SvgValidationResult Valid() => new(true, "Valid SVG");

    public static SvgValidationResult Invalid(
        string message,
        int? lineNumber = null,
        int? columnNumber = null) => new(false, message, lineNumber, columnNumber);
}
