namespace SvgLiveEditor.Models;

public sealed record PreviewDragFileResult(
    bool Succeeded,
    string? Path,
    string? ErrorMessage)
{
    public static PreviewDragFileResult Success(string path) =>
        new(true, path, null);

    public static PreviewDragFileResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
