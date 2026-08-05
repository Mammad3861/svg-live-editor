namespace SvgLiveEditor.Services;

public enum SourceEditorContextCommand
{
    Undo,
    Redo,
    Cut,
    Copy,
    Paste,
    Delete,
    SelectAll
}

public readonly record struct SourceEditorContextMenuItem(
    SourceEditorContextCommand? Command,
    string Header,
    string InputGestureText = "")
{
    public bool IsSeparator => Command is null;
}

public readonly record struct SourceEditorCommandState(
    bool CanUndo,
    bool CanRedo,
    bool HasSelection,
    bool HasText,
    bool CanPasteText,
    bool IsReadOnly,
    bool IsCompositionActive);

public static class SourceEditorContextMenuPolicy
{
    public static IReadOnlyList<SourceEditorContextMenuItem> Items { get; } =
    [
        new(SourceEditorContextCommand.Undo, "Undo", "Ctrl+Z"),
        new(SourceEditorContextCommand.Redo, "Redo", "Ctrl+Y"),
        new(null, string.Empty),
        new(SourceEditorContextCommand.Cut, "Cut", "Ctrl+X"),
        new(SourceEditorContextCommand.Copy, "Copy", "Ctrl+C"),
        new(SourceEditorContextCommand.Paste, "Paste", "Ctrl+V"),
        new(SourceEditorContextCommand.Delete, "Delete", "Del"),
        new(null, string.Empty),
        new(SourceEditorContextCommand.SelectAll, "Select All", "Ctrl+A")
    ];

    public static bool IsEnabled(
        SourceEditorContextCommand command,
        SourceEditorCommandState state)
    {
        bool canModify = !state.IsReadOnly && !state.IsCompositionActive;
        return command switch
        {
            SourceEditorContextCommand.Undo => canModify && state.CanUndo,
            SourceEditorContextCommand.Redo => canModify && state.CanRedo,
            SourceEditorContextCommand.Cut => canModify && state.HasSelection,
            SourceEditorContextCommand.Copy => state.HasSelection,
            SourceEditorContextCommand.Paste => canModify && state.CanPasteText,
            SourceEditorContextCommand.Delete => canModify && state.HasSelection,
            SourceEditorContextCommand.SelectAll => state.HasText,
            _ => false
        };
    }

    public static bool IsOffsetInsideSelection(
        int offset,
        int selectionStart,
        int selectionLength) =>
        selectionLength > 0
        && offset >= selectionStart
        && offset < selectionStart + selectionLength;
}
