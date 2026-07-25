using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewViewportCalculator
{
    public PreviewViewportPosition Capture(
        PreviewScrollPosition scroll,
        double contentWidth,
        double contentHeight,
        double viewportWidth,
        double viewportHeight)
    {
        return new PreviewViewportPosition(
            CaptureAxis(scroll.Left, contentWidth, viewportWidth),
            CaptureAxis(scroll.Top, contentHeight, viewportHeight));
    }

    public PreviewScrollPosition Restore(
        PreviewViewportPosition viewport,
        double contentWidth,
        double contentHeight,
        double viewportWidth,
        double viewportHeight)
    {
        return new PreviewScrollPosition(
            RestoreAxis(viewport.CenterX, contentWidth, viewportWidth),
            RestoreAxis(viewport.CenterY, contentHeight, viewportHeight));
    }

    private static double CaptureAxis(
        double scroll,
        double contentSize,
        double viewportSize)
    {
        if (!double.IsFinite(scroll)
            || !double.IsFinite(contentSize)
            || !double.IsFinite(viewportSize)
            || contentSize <= 0
            || viewportSize <= 0)
        {
            return 0.5;
        }

        double center = (Math.Max(0, scroll) + (viewportSize / 2)) / contentSize;
        return Math.Clamp(center, 0, 1);
    }

    private static double RestoreAxis(
        double normalizedCenter,
        double contentSize,
        double viewportSize)
    {
        if (!double.IsFinite(normalizedCenter)
            || !double.IsFinite(contentSize)
            || !double.IsFinite(viewportSize)
            || contentSize <= 0
            || viewportSize <= 0)
        {
            return 0;
        }

        double maximum = Math.Max(0, contentSize - viewportSize);
        double desired = (Math.Clamp(normalizedCenter, 0, 1) * contentSize)
            - (viewportSize / 2);
        return Math.Clamp(desired, 0, maximum);
    }
}
