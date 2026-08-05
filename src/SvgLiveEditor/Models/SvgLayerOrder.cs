namespace SvgLiveEditor.Models;

public enum SvgLayerOrderCommand
{
    BringToFront,
    BringForward,
    SendBackward,
    SendToBack
}

public sealed record SvgLayerOrderAvailability(
    bool CanExecute,
    string? UnavailableReason = null);

public sealed record SvgLayerPositionInfo(
    bool IsEligible,
    int Position,
    int Count,
    string ParentLabel,
    string BoundaryExplanation,
    string? UnavailableReason = null)
{
    public string DisplayText => IsEligible
        ? $"Layer {Position} of {Count}"
        : string.Empty;
}

public sealed record SvgLayerOrderEditResult(
    bool IsSuccess,
    SourceTextEdit? Edit,
    SvgElementIdentity? PreferredSelection,
    string? ErrorMessage)
{
    public static SvgLayerOrderEditResult Success(
        SourceTextEdit? edit,
        SvgElementIdentity preferredSelection) =>
        new(true, edit, preferredSelection, null);

    public static SvgLayerOrderEditResult Invalid(string message) =>
        new(false, null, null, message);
}
