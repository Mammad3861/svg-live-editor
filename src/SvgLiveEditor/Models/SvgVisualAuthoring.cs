namespace SvgLiveEditor.Models;

public enum SvgCreateElementKind
{
    Rectangle,
    Circle,
    Ellipse,
    Line,
    Text,
    Group
}

public enum SvgCreateDestination
{
    SvgRoot,
    SelectedContext
}

public sealed record SvgAuthoringAvailability(
    bool CanExecute,
    string? UnavailableReason = null);

public sealed record SvgAuthoringEditResult(
    bool IsSuccess,
    SourceTextEdit? Edit,
    SvgElementIdentity? PreferredSelection,
    string? ErrorMessage,
    bool RequiresConfirmation = false,
    string? ConfirmationMessage = null)
{
    public static SvgAuthoringEditResult Success(
        SourceTextEdit edit,
        SvgElementIdentity preferredSelection,
        bool requiresConfirmation = false,
        string? confirmationMessage = null) =>
        new(
            true,
            edit,
            preferredSelection,
            null,
            requiresConfirmation,
            confirmationMessage);

    public static SvgAuthoringEditResult Invalid(string message) =>
        new(false, null, null, message);
}
