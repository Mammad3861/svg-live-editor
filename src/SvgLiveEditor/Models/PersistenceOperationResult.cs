namespace SvgLiveEditor.Models;

public readonly record struct PersistenceOperationResult(
    bool Succeeded,
    string? ErrorMessage)
{
    public static PersistenceOperationResult Success { get; } =
        new(true, null);

    public static PersistenceOperationResult Failure(string message) =>
        new(false, message);
}
