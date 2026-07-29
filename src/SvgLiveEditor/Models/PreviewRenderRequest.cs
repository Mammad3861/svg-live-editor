namespace SvgLiveEditor.Models;

public sealed record PreviewRenderRequest(
    long Revision,
    long SourceRevision,
    string Svg,
    SvgCanvasSize CanvasSize,
    SvgVisualDocument VisualDocument,
    PreviewZoomState ZoomState,
    PreviewViewportPosition Viewport);
