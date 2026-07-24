using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewZoomBridge
{
    private readonly PreviewZoomCalculator _zoomCalculator = new();
    private readonly PreviewScrollCalculator _scrollCalculator = new();

    public PreviewZoomTransition Apply(
        PreviewZoomState currentState,
        SvgCanvasSize canvasSize,
        double fitScale,
        PreviewZoomRequest request)
    {
        PreviewZoomState nextState = request.Direction == PreviewZoomDirection.In
            ? _zoomCalculator.ZoomIn(currentState, fitScale)
            : _zoomCalculator.ZoomOut(currentState, fitScale);
        double nextScale = _zoomCalculator.ResolveScale(nextState, fitScale);
        double renderedWidth = canvasSize.Width * nextScale;
        double renderedHeight = canvasSize.Height * nextScale;
        double contentWidth = Math.Max(
            request.ViewportWidth,
            renderedWidth + (PreviewZoomCalculator.CanvasPadding * 2));
        double contentHeight = Math.Max(
            request.ViewportHeight,
            renderedHeight + (PreviewZoomCalculator.CanvasPadding * 2));
        PreviewScrollPosition scroll = _scrollCalculator.KeepAnchorStable(
            request,
            contentWidth,
            contentHeight);

        return new PreviewZoomTransition(
            nextState,
            scroll,
            renderedWidth,
            renderedHeight);
    }
}
