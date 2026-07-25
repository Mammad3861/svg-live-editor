using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class InspectorSelectionCoordinator
{
    public bool TryGetNavigationSpan(
        InspectorSelectionOrigin origin,
        SourceSpan span,
        bool isIndexCurrent,
        long indexRevision,
        long sourceRevision,
        bool isEditorTextCompositionActive,
        int documentLength,
        out SourceSpan navigationSpan)
    {
        navigationSpan = default;
        if (origin != InspectorSelectionOrigin.ExplicitTreeNavigation
            || !isIndexCurrent
            || indexRevision != sourceRevision
            || isEditorTextCompositionActive
            || span.Start < 0
            || span.Length <= 0
            || span.Start > documentLength
            || span.Length > documentLength - span.Start)
        {
            return false;
        }

        navigationSpan = span;
        return true;
    }
}
