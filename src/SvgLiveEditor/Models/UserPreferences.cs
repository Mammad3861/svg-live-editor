namespace SvgLiveEditor.Models;

public readonly record struct UserPreferences(
    bool WordWrap,
    PreviewZoomState PreviewZoom)
{
    public bool AutoSaveEnabled { get; init; }

    public bool ReopenLastDocumentOnStartup { get; init; } = true;

    public string? LastDocumentPath { get; init; }

    public static UserPreferences Default { get; } = new(
        WordWrap: true,
        PreviewZoom: PreviewZoomState.Fit);
}
