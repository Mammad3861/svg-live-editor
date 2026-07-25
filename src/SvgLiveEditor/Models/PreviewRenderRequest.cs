namespace SvgLiveEditor.Models;

public sealed record PreviewRenderRequest(
    long Revision,
    string Svg,
    SvgCanvasSize CanvasSize,
    PreviewZoomState ZoomState,
    PreviewViewportPosition Viewport);
