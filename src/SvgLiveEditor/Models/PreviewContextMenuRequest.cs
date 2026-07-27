namespace SvgLiveEditor.Models;

public readonly record struct PreviewContextMenuRequest(
    double X,
    double Y,
    double ViewportWidth,
    double ViewportHeight);
