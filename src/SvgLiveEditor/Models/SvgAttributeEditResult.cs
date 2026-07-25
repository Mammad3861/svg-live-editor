namespace SvgLiveEditor.Models;

public sealed record SvgAttributeEditResult(
    bool IsSuccess,
    SourceTextEdit? Edit,
    string? ErrorMessage)
{
    public static SvgAttributeEditResult Success(SourceTextEdit? edit) =>
        new(true, edit, null);

    public static SvgAttributeEditResult Invalid(string message) =>
        new(false, null, message);
}
