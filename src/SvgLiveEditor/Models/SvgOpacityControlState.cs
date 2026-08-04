namespace SvgLiveEditor.Models;

public sealed record SvgOpacityControlState(
    bool IsVisible,
    bool IsEnabled,
    double Percent,
    string? UnavailableReason = null,
    string? Advisory = null)
{
    public static SvgOpacityControlState Hidden { get; } =
        new(false, false, 100);
}
