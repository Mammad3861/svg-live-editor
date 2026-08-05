namespace SvgLiveEditor.Services;

public enum InspectorUndoShortcut
{
    Undo,
    Redo
}

public enum InspectorUndoShortcutRoute
{
    Ignore,
    FocusedControl,
    DocumentUndo,
    DocumentRedo
}

public readonly record struct InspectorUndoFocusState(
    bool IsSourceEditorFocused,
    bool IsPropertiesFocused,
    bool HasUncommittedValue,
    bool HasLocalRedo,
    bool IsTextCompositionActive);

public static class InspectorUndoShortcutRouter
{
    public static InspectorUndoShortcutRoute Resolve(
        InspectorUndoShortcut shortcut,
        InspectorUndoFocusState focus)
    {
        if (focus.IsSourceEditorFocused)
        {
            return GetDocumentRoute(shortcut);
        }

        if (!focus.IsPropertiesFocused)
        {
            return InspectorUndoShortcutRoute.Ignore;
        }

        if (focus.IsTextCompositionActive
            || focus.HasUncommittedValue
            || shortcut == InspectorUndoShortcut.Redo && focus.HasLocalRedo)
        {
            return InspectorUndoShortcutRoute.FocusedControl;
        }

        return GetDocumentRoute(shortcut);
    }

    private static InspectorUndoShortcutRoute GetDocumentRoute(
        InspectorUndoShortcut shortcut) =>
        shortcut == InspectorUndoShortcut.Undo
            ? InspectorUndoShortcutRoute.DocumentUndo
            : InspectorUndoShortcutRoute.DocumentRedo;
}
