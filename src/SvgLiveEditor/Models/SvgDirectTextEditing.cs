namespace SvgLiveEditor.Models;

public sealed record SvgDirectTextContent(
    SvgElementNode Element,
    SourceSpan ContentSpan,
    string Text);

public sealed record SvgDirectTextAnalysis(
    bool IsEligible,
    SvgDirectTextContent? Content,
    string? UnavailableReason)
{
    public static SvgDirectTextAnalysis Eligible(
        SvgDirectTextContent content) =>
        new(true, content, null);

    public static SvgDirectTextAnalysis Rejected(string reason) =>
        new(false, null, reason);
}

public readonly record struct SvgTextResizeCandidate(
    double FontSize,
    SvgVisualShapeGeometry Geometry);

public enum PreviewTextEditAction
{
    Begin,
    Commit,
    Cancel
}

public sealed record PreviewTextEditMessage(
    PreviewTextEditAction Action,
    long SourceRevision,
    string SelectionId,
    string? SessionId,
    string? Value,
    bool RestorePreviewFocus = false);
