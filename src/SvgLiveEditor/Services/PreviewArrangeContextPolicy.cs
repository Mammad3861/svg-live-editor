using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class PreviewArrangeContextPolicy
{
    public static bool IsCurrent(
        PreviewContextMenuRequest request,
        long? visibleSourceRevision,
        long currentSourceRevision,
        string? currentSelectionId,
        bool selectionIdentityMatches) =>
        request.SourceRevision == visibleSourceRevision
        && request.SourceRevision == currentSourceRevision
        && request.SelectionId is { Length: 32 }
        && request.SelectionId.Equals(
            currentSelectionId,
            StringComparison.Ordinal)
        && selectionIdentityMatches;
}
