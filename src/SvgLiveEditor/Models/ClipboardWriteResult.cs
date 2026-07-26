namespace SvgLiveEditor.Models;

public sealed record ClipboardWriteResult(
    bool Succeeded,
    int Attempts,
    string? ErrorMessage)
{
    public static ClipboardWriteResult Success(int attempts) =>
        new(true, attempts, null);

    public static ClipboardWriteResult Failure(
        int attempts,
        string errorMessage) =>
        new(false, attempts, errorMessage);
}
