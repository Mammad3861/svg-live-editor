namespace SvgLiveEditor.Services;

public sealed class SourceRevisionTracker
{
    public long Current { get; private set; }

    public long Advance()
    {
        Current = checked(Current + 1);
        return Current;
    }

    public bool IsCurrent(long revision) => revision == Current;
}
