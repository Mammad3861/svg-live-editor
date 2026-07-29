namespace SvgLiveEditor.Models;

public readonly record struct SafeDocumentPathResult(
    bool IsAllowed,
    string? FullPath,
    string StatusMessage)
{
    public static SafeDocumentPathResult Allowed(string fullPath) =>
        new(true, fullPath, string.Empty);

    public static SafeDocumentPathResult Blocked(string statusMessage) =>
        new(false, null, statusMessage);
}
