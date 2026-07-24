namespace SvgLiveEditor.Models;

public readonly record struct UserPreferences(
    bool WordWrap,
    PreviewZoomState PreviewZoom)
{
    public static UserPreferences Default { get; } = new(
        WordWrap: true,
        PreviewZoom: PreviewZoomState.Fit);
}
