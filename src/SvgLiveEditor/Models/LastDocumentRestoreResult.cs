namespace SvgLiveEditor.Models;

public sealed record LastDocumentRestoreResult(
    bool IsRestored,
    bool ShouldClearPath,
    string? Source,
    string? Path)
{
    public static LastDocumentRestoreResult NotRequested { get; } =
        new(false, false, null, null);

    public static LastDocumentRestoreResult Unavailable { get; } =
        new(false, true, null, null);

    public static LastDocumentRestoreResult Restored(
        string source,
        string path) =>
        new(true, false, source, path);
}
