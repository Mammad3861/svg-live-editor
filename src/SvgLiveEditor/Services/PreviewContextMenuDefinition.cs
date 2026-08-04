namespace SvgLiveEditor.Services;

public enum PreviewContextMenuCommand
{
    CopyPreviewAsPng,
    Fit,
    ResetZoom,
    BringToFront,
    BringForward,
    SendBackward,
    SendToBack
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
            new(PreviewContextMenuCommand.ResetZoom, "Reset Zoom"),
            new(PreviewContextMenuCommand.BringToFront, "Bring to Front"),
            new(PreviewContextMenuCommand.BringForward, "Bring Forward"),
            new(PreviewContextMenuCommand.SendBackward, "Send Backward"),
            new(PreviewContextMenuCommand.SendToBack, "Send to Back")
        });
}
