namespace SvgLiveEditor.Models;

public readonly record struct AutoSaveValidationDecision(
    bool CanWrite,
    string StatusMessage);
