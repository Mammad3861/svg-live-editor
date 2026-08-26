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
        if (origin is not (
                InspectorSelectionOrigin.ExplicitTreeNavigation
                or InspectorSelectionOrigin.PreviewNavigation)
            || !isIndexCurrent
            || indexRevision != sourceRevision
            || isEditorTextCompositionActive
            || span.Start < 0
            || span.Length < 0
            || span.Start > documentLength
            || span.Length > documentLength - span.Start)
        {
            return false;
        }

        navigationSpan = span;
        return true;
    }

    public SvgElementIdentity? ResolveRefreshSelection(
        SvgElementIdentity? preferredSelection,
        bool selectionRestoreApplied,
        SvgElementIdentity? restoredPrimary,
        SvgElementIdentity? currentSelection,
        SvgElementIdentity? reconciledPrimary,
        SvgDocumentIndex? currentDocument,
        out bool selectFirstRootWhenSelectionIsEmpty)
    {
        if (selectionRestoreApplied)
        {
            selectFirstRootWhenSelectionIsEmpty = false;
            return ResolveCurrent(restoredPrimary, currentDocument);
        }

        SvgElementIdentity? resolved =
            ResolveCurrent(preferredSelection, currentDocument)
            ?? ResolveCurrent(currentSelection, currentDocument)
            ?? ResolveCurrent(reconciledPrimary, currentDocument);
        selectFirstRootWhenSelectionIsEmpty = resolved is null;
        return resolved;
    }

    private static SvgElementIdentity? ResolveCurrent(
        SvgElementIdentity? selection,
        SvgDocumentIndex? document) =>
        selection is null || document is null
            ? null
            : document.FindBestMatch(selection)?.Identity;
}
