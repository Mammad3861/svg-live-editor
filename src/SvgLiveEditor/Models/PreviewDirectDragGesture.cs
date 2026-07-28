namespace SvgLiveEditor.Models;

public enum PreviewPointerGestureAction
{
    None,
    Pan,
    OutboundDrag
}

public readonly record struct PreviewPointerGestureInput(
    int Button,
    bool StartedOnArtwork,
    bool IsPrimary,
    bool IsMouse,
    bool ControlHeld,
    bool ShiftHeld,
    bool AltHeld,
    bool MetaHeld,
    bool SpaceHeld,
    bool PanModeEnabled);

public readonly record struct PreviewDirectDragArmRequest(
    string GestureId,
    PreviewPointerGestureInput Gesture,
    double X,
    double Y,
    double ViewportWidth,
    double ViewportHeight);

public enum PreviewDirectDragSignalAction
{
    Start,
    Cancel
}

public readonly record struct PreviewDirectDragSignal(
    PreviewDirectDragSignalAction Action,
    string GestureId,
    double X,
    double Y,
    double ViewportWidth,
    double ViewportHeight);

public enum PreviewDragRequestOrigin
{
    Toolbar,
    Artwork
}
