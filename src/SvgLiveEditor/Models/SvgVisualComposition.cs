namespace SvgLiveEditor.Models;

public sealed record SvgMultiSelectionState(
    long SourceRevision,
    IReadOnlyList<SvgElementIdentity> Identities,
    SvgElementIdentity? Primary,
    SvgElementIdentity? Anchor)
{
    public static SvgMultiSelectionState Empty(long sourceRevision) =>
        new(sourceRevision, [], null, null);
}

public sealed record SvgMultiSelectionChange(
    bool IsSuccess,
    SvgMultiSelectionState State,
    string? ErrorMessage)
{
    public static SvgMultiSelectionChange Success(
        SvgMultiSelectionState state) =>
        new(true, state, null);

    public static SvgMultiSelectionChange Invalid(
        SvgMultiSelectionState state,
        string message) =>
        new(false, state, message);
}

public readonly record struct SvgVisualMoveDelta(
    SvgVisualElement Element,
    double DeltaX,
    double DeltaY);

public enum SvgLayoutCommand
{
    AlignLeft,
    AlignHorizontalCenters,
    AlignRight,
    AlignTop,
    AlignVerticalCenters,
    AlignBottom,
    DistributeHorizontally,
    DistributeVertically
}

public enum PreviewAlignmentGuideOrientation
{
    Vertical,
    Horizontal
}

public readonly record struct PreviewAlignmentGuide(
    PreviewAlignmentGuideOrientation Orientation,
    double Position,
    double Start,
    double End);

public sealed record SvgSnapResult(
    double DeltaX,
    double DeltaY,
    IReadOnlyList<PreviewAlignmentGuide> Guides);

