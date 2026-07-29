namespace SvgLiveEditor.Models;

public sealed record RecoverySnapshot(
    int SchemaVersion,
    string SnapshotId,
    string? OriginalPath,
    string DisplayName,
    string Source,
    string SourceSha256,
    long Revision,
    DateTimeOffset SavedUtc,
    bool IsNamed)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record RecoveryCandidate(
    RecoverySnapshot Snapshot,
    string? RestorablePath);

public enum RecoveryDialogChoice
{
    Restore,
    Discard,
    Skip
}
