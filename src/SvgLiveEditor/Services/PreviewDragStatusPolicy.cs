using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class PreviewDragStatusPolicy
{
    public static string Started(
        PreviewPngSourceState sourceState,
        PreviewPngSize size)
    {
        string dimensions = $"{size.Width} \u00D7 {size.Height}";
        return sourceState switch
        {
            PreviewPngSourceState.CurrentInvalid =>
                $"Dragging the last valid preview; current source is invalid \u00B7 {dimensions}",
            PreviewPngSourceState.PendingValidation =>
                $"Dragging the last validated preview; current source is still validating \u00B7 {dimensions}",
            _ => $"Dragging preview image \u00B7 {dimensions}"
        };
    }
}
