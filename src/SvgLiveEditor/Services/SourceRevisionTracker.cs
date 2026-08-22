using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SourceRevisionTracker
{
    public long Current { get; private set; }

    public SourceChangeOrigin CurrentOrigin { get; private set; } =
        SourceChangeOrigin.DocumentLoad;

    public long Advance(
        SourceChangeOrigin origin = SourceChangeOrigin.SourceEditor)
    {
        Current = checked(Current + 1);
        CurrentOrigin = origin;
        return Current;
    }

    public bool IsCurrent(long revision) => revision == Current;

    public bool CanSynchronizeSourceCaret(
        long indexedRevision,
        bool sourceEditorHasKeyboardFocus,
        bool isTextCompositionActive,
        bool selectionRestoreApplied) =>
        IsCurrent(indexedRevision)
        && CurrentOrigin == SourceChangeOrigin.SourceEditor
        && sourceEditorHasKeyboardFocus
        && !isTextCompositionActive
        && !selectionRestoreApplied;
}
