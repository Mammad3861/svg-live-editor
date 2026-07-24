namespace SvgLiveEditor.Models;

public enum PreviewZoomDirection
{
    In,
    Out
}

public readonly record struct PreviewZoomRequest(
    PreviewZoomDirection Direction,
    double ContentX,
    double ContentY,
    double AnchorX,
    double AnchorY,
    double ViewportWidth,
    double ViewportHeight);

public readonly record struct PreviewScrollPosition(double Left, double Top)
{
    public static PreviewScrollPosition Origin { get; } = new(0, 0);
}
