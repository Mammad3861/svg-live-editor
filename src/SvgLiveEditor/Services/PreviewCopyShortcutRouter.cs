namespace SvgLiveEditor.Services;

public enum CopyShortcutAction
{
    LeaveNativeCopy,
    CopyPreviewAsPng,
    Ignore
}

public readonly record struct CopyFocusState(
    bool PreviewHasKeyboardFocus,
    bool SourceEditorHasKeyboardFocus,
    bool TextFieldHasKeyboardFocus,
    bool PointerIsOverPreview);

public static class PreviewCopyShortcutRouter
{
    public static CopyShortcutAction Resolve(CopyFocusState focus)
    {
        if (focus.SourceEditorHasKeyboardFocus
            || focus.TextFieldHasKeyboardFocus)
        {
            return CopyShortcutAction.LeaveNativeCopy;
        }

        return focus.PreviewHasKeyboardFocus
            ? CopyShortcutAction.CopyPreviewAsPng
            : CopyShortcutAction.Ignore;
    }
}
