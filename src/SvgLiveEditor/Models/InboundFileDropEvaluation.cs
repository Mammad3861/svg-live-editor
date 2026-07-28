namespace SvgLiveEditor.Models;

public enum InboundFileDropRejection
{
    None,
    EmptyPayload,
    UnreadablePayload,
    MultipleFiles,
    NotLocalFile,
    Directory,
    Shortcut,
    UnsupportedExtension,
    MissingFile,
    ReparsePoint,
    OversizedFile,
    UnreadableFile
}

public sealed record InboundFileDropEvaluation(
    bool IsAccepted,
    string? FullPath,
    string? DisplayFileName,
    InboundFileDropRejection Rejection,
    string StatusMessage)
{
    public static InboundFileDropEvaluation Accepted(
        string fullPath,
        string displayFileName) =>
        new(
            true,
            fullPath,
            displayFileName,
            InboundFileDropRejection.None,
            $"Drop {displayFileName} to open");

    public static InboundFileDropEvaluation Rejected(
        InboundFileDropRejection rejection,
        string statusMessage) =>
        new(false, null, null, rejection, statusMessage);
}
