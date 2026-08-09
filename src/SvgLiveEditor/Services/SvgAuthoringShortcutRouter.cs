using System.Windows.Input;

namespace SvgLiveEditor.Services;

public enum SvgAuthoringShortcutAction
{
    None,
    Duplicate,
    Delete
}

public static class SvgAuthoringShortcutRouter
{
    public static SvgAuthoringShortcutAction Resolve(
        ModifierKeys modifiers,
        Key key,
        bool authoringSurfaceHasKeyboardFocus,
        bool editableControlHasKeyboardFocus,
        bool isTextCompositionActive)
    {
        if (!authoringSurfaceHasKeyboardFocus
            || editableControlHasKeyboardFocus
            || isTextCompositionActive)
        {
            return SvgAuthoringShortcutAction.None;
        }

        return (modifiers, key) switch
        {
            (ModifierKeys.Control, Key.D) =>
                SvgAuthoringShortcutAction.Duplicate,
            (ModifierKeys.None, Key.Delete or Key.Back) =>
                SvgAuthoringShortcutAction.Delete,
            _ => SvgAuthoringShortcutAction.None
        };
    }
}
