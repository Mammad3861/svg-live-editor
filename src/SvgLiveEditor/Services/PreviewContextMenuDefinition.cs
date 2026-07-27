namespace SvgLiveEditor.Services;

public enum PreviewContextMenuCommand
{
    CopyPreviewAsPng,
    Fit,
    ResetZoom
}

public readonly record struct PreviewContextMenuItem(
    PreviewContextMenuCommand Command,
    string Header);

public static class PreviewContextMenuDefinition
{
    public static IReadOnlyList<PreviewContextMenuItem> Items { get; } =
        Array.AsReadOnly(
        new PreviewContextMenuItem[]
        {
            new(
                PreviewContextMenuCommand.CopyPreviewAsPng,
                "Copy Preview as PNG"),
            new(PreviewContextMenuCommand.Fit, "Fit"),
            new(PreviewContextMenuCommand.ResetZoom, "Reset Zoom")
        });
}
