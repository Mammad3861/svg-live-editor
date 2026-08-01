namespace SvgLiveEditor.Models;

public enum SvgVisualElementKind
{
    Unsupported,
    Rect,
    Circle,
    Ellipse,
    Line,
    Text
}

public readonly record struct SvgVisualPoint(double X, double Y);

public readonly record struct SvgVisualBounds(
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}

public readonly record struct SvgPreserveAspectRatio(
    bool IsNone,
    double AlignX,
    double AlignY,
    bool IsSlice,
    string SvgValue)
{
    public static SvgPreserveAspectRatio Default { get; } =
        new(false, 0.5, 0.5, false, "xMidYMid meet");
}

public readonly record struct SvgVisualViewport(
    double MinX,
    double MinY,
    double Width,
    double Height,
    SvgPreserveAspectRatio PreserveAspectRatio);

public sealed record SvgVisualShapeGeometry(
    SvgVisualElementKind Kind,
    double X1,
    double Y1,
    double X2,
    double Y2)
{
    public SvgVisualBounds Bounds => new(
        Math.Min(X1, X2),
        Math.Min(Y1, Y2),
        Math.Max(X1, X2),
        Math.Max(Y1, Y2));
}

public sealed record SvgVisualElement(
    SvgElementNode SourceElement,
    SvgVisualElementKind Kind,
    SvgVisualShapeGeometry? Geometry,
    string? UnsupportedReason,
    SvgVisualTextMeasurementSpec? TextMeasurement = null,
    bool BlocksLowerVisualHits = false)
{
    public bool IsSelectable => Geometry is not null;

    public bool IsMovable =>
        Geometry is not null && string.IsNullOrWhiteSpace(UnsupportedReason);
}

public readonly record struct SvgVisualHitTestResult(
    SvgVisualElement? Element,
    SvgVisualElement? Blocker)
{
    public static SvgVisualHitTestResult None { get; } = new(null, null);
}

public sealed record SvgVisualTextMeasurementSpec(
    int Index,
    string Text,
    double X,
    double Y,
    double FontSize,
    string FontFamily,
    string FontWeight,
    string FontStyle,
    string TextAnchor,
    string Direction,
    string UnicodeBidi);

public sealed record SvgVisualTextMeasurementResult(
    int Index,
    bool IsSuccess,
    SvgVisualBounds? Bounds);

public sealed record PendingPreviewTextMeasurement(
    string Token,
    long SourceRevision,
    string RequestId,
    IReadOnlyList<int> ExpectedIndices);

public sealed class SvgVisualDocument
{
    public SvgVisualDocument(
        SvgVisualViewport viewport,
        IReadOnlyList<SvgVisualElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        Viewport = viewport;
        Elements = elements;
    }

    public SvgVisualViewport Viewport { get; }

    public IReadOnlyList<SvgVisualElement> Elements { get; }

    public SvgVisualElement? FindElement(SvgElementIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return Elements.FirstOrDefault(element =>
            element.SourceElement.Identity == identity);
    }
}

public readonly record struct PreviewImageMetrics(
    double Left,
    double Top,
    double Width,
    double Height);

public readonly record struct SvgMappedPreviewPoint(
    SvgVisualPoint Point,
    double HitTolerance);

public enum PreviewVisualPointerPhase
{
    Down,
    Move,
    Up,
    Cancel
}

public readonly record struct PreviewVisualPointerMessage(
    PreviewVisualPointerPhase Phase,
    string GestureId,
    long SourceRevision,
    SvgVisualPoint ViewportPoint,
    double ViewportWidth,
    double ViewportHeight,
    PreviewImageMetrics Image,
    int Button,
    int Buttons,
    bool ControlHeld,
    bool ShiftHeld,
    bool AltHeld,
    bool MetaHeld,
    bool SpaceHeld);

public readonly record struct PreviewVisualNudgeRequest(
    long SourceRevision,
    double DeltaX,
    double DeltaY);

public readonly record struct PreviewVisualSelection(
    SvgVisualElementKind Kind,
    SvgVisualShapeGeometry Geometry,
    double DeltaX,
    double DeltaY);

public readonly record struct VisualEditingReadiness(
    bool IsPanModeEnabled,
    bool IsCurrentSourceValid,
    bool IsInspectorIndexCurrent,
    long InspectorRevision,
    long CurrentSourceRevision,
    long? LastValidVisualRevision,
    long? VisiblePreviewRevision,
    bool IsNavigationPending);
