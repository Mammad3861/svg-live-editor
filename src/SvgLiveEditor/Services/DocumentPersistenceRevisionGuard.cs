namespace SvgLiveEditor.Services;

public static class DocumentPersistenceRevisionGuard
{
    public static bool IsCurrent(
        long capturedSession,
        long currentSession,
        long capturedRevision,
        long currentRevision,
        string capturedSource,
        string currentSource,
        string? capturedPath,
        string? currentPath)
    {
        return capturedSession == currentSession
            && capturedRevision == currentRevision
            && capturedSource.Equals(
                currentSource,
                StringComparison.Ordinal)
            && string.Equals(
                capturedPath,
                currentPath,
                StringComparison.OrdinalIgnoreCase);
    }
}
