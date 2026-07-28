namespace SvgLiveEditor.Services;

public enum FileDropOverlayEvent
{
    SupportedDrag,
    Drop,
    DragLeftWindow,
    Escape,
    WindowDeactivated,
    Cancelled
}

public sealed record FileDropOverlayPresentation(
    bool IsVisible,
    string FileName)
{
    public static FileDropOverlayPresentation Hidden { get; } =
        new(false, string.Empty);
}

public sealed class FileDropOverlayState
{
    public FileDropOverlayPresentation Current { get; private set; } =
        FileDropOverlayPresentation.Hidden;

    public FileDropOverlayPresentation Transition(
        FileDropOverlayEvent overlayEvent,
        string? fileName = null)
    {
        Current = overlayEvent == FileDropOverlayEvent.SupportedDrag
            && !string.IsNullOrWhiteSpace(fileName)
                ? new FileDropOverlayPresentation(true, fileName)
                : FileDropOverlayPresentation.Hidden;
        return Current;
    }
}
