namespace SvgLiveEditor.Services;

public enum PreviewUpdateKind
{
    Source,
    Zoom
}

public readonly record struct PreviewUpdateDecision(
    bool RequiresNavigation,
    bool ShowsFullLoadingState);

public sealed class PreviewUpdatePolicy
{
    public PreviewUpdateDecision Decide(
        PreviewUpdateKind kind,
        bool hasVisiblePreview)
    {
        return kind switch
        {
            PreviewUpdateKind.Zoom => new(
                RequiresNavigation: false,
                ShowsFullLoadingState: false),
            PreviewUpdateKind.Source => new(
                RequiresNavigation: true,
                ShowsFullLoadingState: !hasVisiblePreview),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
