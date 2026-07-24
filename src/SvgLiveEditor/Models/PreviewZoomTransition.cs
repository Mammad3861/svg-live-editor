namespace SvgLiveEditor.Models;

public readonly record struct PreviewZoomTransition(
    PreviewZoomState State,
    PreviewScrollPosition Scroll,
    double RenderedWidth,
    double RenderedHeight);
