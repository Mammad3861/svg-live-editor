namespace SvgLiveEditor.Models;

public readonly record struct NativeScreenPoint(int X, int Y);

public readonly record struct NativeHorizontalWheelInput(
    short Delta,
    int KeyState,
    NativeScreenPoint ScreenPoint)
{
    public bool ControlHeld => (KeyState & 0x0008) != 0;
}

public enum PreviewNativeInputState
{
    Loading,
    Ready,
    Error,
    Disposed
}

public readonly record struct PreviewNativeScrollContext(
    PreviewNativeInputState State,
    bool IsNavigating,
    bool IsPointerOverPreview,
    string? NavigationToken,
    double ViewportWidth);

public readonly record struct PreviewNativeScrollRequest(
    string NavigationToken,
    double DeltaX);
