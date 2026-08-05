namespace SvgLiveEditor.Models;

public enum SvgLayerDropPlacement
{
    Before,
    After
}

public sealed record SvgLayerVisibilityState(
    bool IsVisible,
    bool IsDirectlyHidden,
    bool IsHiddenByAncestor,
    bool CanToggle,
    string? UnavailableReason = null);

public sealed record SvgLayerItem(
    string OpaqueId,
    SvgElementNode Element,
    string Label,
    bool IsGroup,
    bool IsInspectionOnly,
    bool IsLocked,
    bool IsEffectivelyLocked,
    SvgLayerVisibilityState Visibility,
    IReadOnlyList<SvgLayerItem> Children);

public sealed record SvgLayerWorkspace(
    IReadOnlyList<SvgLayerItem> Roots,
    IReadOnlyDictionary<string, SvgLayerItem> ItemsByPath,
    IReadOnlyDictionary<string, SvgLayerItem> ItemsByOpaqueId);

public sealed record SvgLayerMoveEditResult(
    bool IsSuccess,
    SourceTextEdit? Edit,
    SvgElementIdentity? PreferredSelection,
    string? ErrorMessage)
{
    public static SvgLayerMoveEditResult Success(
        SourceTextEdit edit,
        SvgElementIdentity preferredSelection) =>
        new(true, edit, preferredSelection, null);

    public static SvgLayerMoveEditResult Invalid(string message) =>
        new(false, null, null, message);
}

public sealed record SvgLayerVisibilityEditResult(
    bool IsSuccess,
    SourceTextEdit? Edit,
    bool OwnsHiddenAttributeAfterEdit,
    string? ErrorMessage)
{
    public static SvgLayerVisibilityEditResult Success(
        SourceTextEdit? edit,
        bool ownsHiddenAttributeAfterEdit) =>
        new(true, edit, ownsHiddenAttributeAfterEdit, null);

    public static SvgLayerVisibilityEditResult Invalid(string message) =>
        new(false, null, false, message);
}
