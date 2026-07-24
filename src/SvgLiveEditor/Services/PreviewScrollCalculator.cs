using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewScrollCalculator
{
    public PreviewScrollPosition KeepAnchorStable(
        PreviewZoomRequest request,
        double contentWidth,
        double contentHeight)
    {
        double left = CalculateAxis(
            request.ContentX,
            request.AnchorX,
            contentWidth,
            request.ViewportWidth);
        double top = CalculateAxis(
            request.ContentY,
            request.AnchorY,
            contentHeight,
            request.ViewportHeight);
        return new PreviewScrollPosition(left, top);
    }

    private static double CalculateAxis(
        double normalizedContentPosition,
        double pointerPosition,
        double contentSize,
        double viewportSize)
    {
        if (!double.IsFinite(normalizedContentPosition)
            || !double.IsFinite(pointerPosition)
            || !double.IsFinite(contentSize)
            || !double.IsFinite(viewportSize)
            || contentSize <= 0
            || viewportSize <= 0)
        {
            return 0;
        }

        double maximum = Math.Max(0, contentSize - viewportSize);
        double desired = (Math.Clamp(normalizedContentPosition, 0, 1) * contentSize)
            - pointerPosition;
        return Math.Clamp(desired, 0, maximum);
    }
}
