namespace SvgLiveEditor.Models;

public readonly record struct PreviewPngSize(int Width, int Height)
{
    public long PixelCount => (long)Width * Height;
}

public enum PreviewPngSourceState
{
    CurrentValid,
    CurrentInvalid,
    PendingValidation
}

public sealed record PreviewPngCopyPlan(
    PreviewPngSize Size,
    PreviewPngSourceState SourceState)
{
    public bool UsesLastValidPreview =>
        SourceState != PreviewPngSourceState.CurrentValid;
}

public sealed record PreviewPngPayload(
    PreviewPngSize Size,
    byte[] Bytes);

public enum PreviewPngRequestPurpose
{
    ClipboardCopy,
    DragOut
}

public sealed record PendingPreviewPngRequest(
    string BridgeToken,
    string RequestId,
    PreviewPngCopyPlan Plan,
    PreviewPngRequestPurpose Purpose);
