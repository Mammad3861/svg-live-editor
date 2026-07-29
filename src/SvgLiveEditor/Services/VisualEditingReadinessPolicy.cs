using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class VisualEditingReadinessPolicy
{
    public bool IsReady(VisualEditingReadiness state)
    {
        return !state.IsPanModeEnabled
            && state.IsCurrentSourceValid
            && state.IsInspectorIndexCurrent
            && state.InspectorRevision == state.CurrentSourceRevision
            && state.LastValidVisualRevision
                == state.CurrentSourceRevision
            && state.VisiblePreviewRevision
                == state.CurrentSourceRevision
            && !state.IsNavigationPending;
    }
}
