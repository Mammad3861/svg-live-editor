namespace SvgLiveEditor.Services;

public static class RecoveryRevisionCalculator
{
    public static long Calculate(
        long baselineRevision,
        long loadedSourceRevision,
        long currentSourceRevision)
    {
        if (baselineRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baselineRevision));
        }

        if (currentSourceRevision < loadedSourceRevision)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentSourceRevision));
        }

        return checked(
            baselineRevision
            + (currentSourceRevision - loadedSourceRevision));
    }
}
