namespace SvgLiveEditor.Services;

public static class DocumentPersistencePolicy
{
    public static readonly TimeSpan RecoveryDelay =
        TimeSpan.FromMilliseconds(1500);

    public static readonly TimeSpan AutoSaveDelay =
        TimeSpan.FromSeconds(2);

    public static bool ShouldScheduleAutoSave(
        bool autoSaveEnabled,
        bool documentIsEligible,
        bool isModified,
        string? currentPath)
    {
        return autoSaveEnabled
            && documentIsEligible
            && isModified
            && !string.IsNullOrWhiteSpace(currentPath);
    }
}
