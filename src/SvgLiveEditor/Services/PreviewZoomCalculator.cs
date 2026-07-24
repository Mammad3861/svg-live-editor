using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewZoomCalculator
{
    public const double MinimumScale = 0.25;
    public const double MaximumScale = 5.0;
    public const double ScaleStep = 0.25;
    public const double CanvasPadding = 24.0;
    private const double ViewportRoundingAllowance = 2.0;

    public double CalculateFitScale(
        SvgCanvasSize canvasSize,
        double viewportWidth,
        double viewportHeight,
        double dpiScaleX = 1.0,
        double dpiScaleY = 1.0)
    {
        if (!IsPositiveFinite(canvasSize.Width)
            || !IsPositiveFinite(canvasSize.Height)
            || !IsPositiveFinite(viewportWidth)
            || !IsPositiveFinite(viewportHeight)
            || !IsPositiveFinite(dpiScaleX)
            || !IsPositiveFinite(dpiScaleY))
        {
            return 1.0;
        }

        double reservedSpace = (CanvasPadding * 2) + ViewportRoundingAllowance;
        double availableWidth = Math.Max(1.0, (viewportWidth / dpiScaleX) - reservedSpace);
        double availableHeight = Math.Max(1.0, (viewportHeight / dpiScaleY) - reservedSpace);
        return Math.Min(
            availableWidth / canvasSize.Width,
            availableHeight / canvasSize.Height);
    }

    public double ResolveScale(PreviewZoomState state, double fitScale)
    {
        return state.Mode == PreviewZoomMode.Fit
            ? fitScale
            : Math.Clamp(state.ManualScale, MinimumScale, MaximumScale);
    }

    public PreviewZoomState ZoomIn(PreviewZoomState state, double fitScale)
    {
        double currentScale = ResolveScale(state, fitScale);
        double nextScale = state.Mode == PreviewZoomMode.Fit
            ? (Math.Floor(currentScale / ScaleStep) + 1) * ScaleStep
            : currentScale + ScaleStep;

        return ToManualState(nextScale);
    }

    public PreviewZoomState ZoomOut(PreviewZoomState state, double fitScale)
    {
        double currentScale = ResolveScale(state, fitScale);
        double nextScale = state.Mode == PreviewZoomMode.Fit
            ? (Math.Ceiling(currentScale / ScaleStep) - 1) * ScaleStep
            : currentScale - ScaleStep;

        return ToManualState(nextScale);
    }

    public PreviewZoomState Reset() => PreviewZoomState.At100Percent;

    public PreviewZoomState Fit() => PreviewZoomState.Fit;

    private static PreviewZoomState ToManualState(double scale)
    {
        return new PreviewZoomState(
            PreviewZoomMode.Manual,
            Math.Clamp(scale, MinimumScale, MaximumScale));
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0;
    }
}
