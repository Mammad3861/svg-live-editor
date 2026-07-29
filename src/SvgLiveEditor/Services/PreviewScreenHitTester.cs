using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewScreenHitTester
{
    public bool Contains(
        NativeScreenPoint pointer,
        double previewLeftPixels,
        double previewTopPixels,
        double previewWidthDips,
        double previewHeightDips,
        double dpiScaleX,
        double dpiScaleY)
    {
        if (!double.IsFinite(previewLeftPixels)
            || !double.IsFinite(previewTopPixels)
            || !double.IsFinite(previewWidthDips)
            || previewWidthDips <= 0
            || !double.IsFinite(previewHeightDips)
            || previewHeightDips <= 0
            || !double.IsFinite(dpiScaleX)
            || dpiScaleX <= 0
            || !double.IsFinite(dpiScaleY)
            || dpiScaleY <= 0)
        {
            return false;
        }

        double right = previewLeftPixels + (previewWidthDips * dpiScaleX);
        double bottom = previewTopPixels + (previewHeightDips * dpiScaleY);
        return pointer.X >= previewLeftPixels
            && pointer.X < right
            && pointer.Y >= previewTopPixels
            && pointer.Y < bottom;
    }
}
