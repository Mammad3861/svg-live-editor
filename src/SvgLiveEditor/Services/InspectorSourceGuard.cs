namespace SvgLiveEditor.Services;

public sealed class InspectorSourceGuard
{
    public bool CanUseIndex(
        bool isIndexCurrent,
        long indexRevision,
        long sourceRevision,
        bool isEditorTextCompositionActive)
    {
        return isIndexCurrent
            && indexRevision == sourceRevision
            && !isEditorTextCompositionActive;
    }
}
