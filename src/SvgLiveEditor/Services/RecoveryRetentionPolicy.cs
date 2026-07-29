namespace SvgLiveEditor.Services;

public static class RecoveryRetentionPolicy
{
    public static bool CanKeep(
        int currentCount,
        long currentBytes,
        long nextFileBytes)
    {
        if (currentCount < 0
            || currentBytes < 0
            || nextFileBytes <= 0)
        {
            return false;
        }

        return currentCount
                < RecoverySnapshotStore.MaximumSnapshotCount
            && nextFileBytes
                <= RecoverySnapshotStore.MaximumTotalBytes - currentBytes;
    }
}
