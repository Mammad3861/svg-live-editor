using System.Windows.Input;

namespace SvgLiveEditor.Services;

public enum SvgLayoutShortcutAction
{
    None,
    Group,
    Ungroup
}

public static class SvgLayoutShortcutRouter
{
    public static SvgLayoutShortcutAction Resolve(
        ModifierKeys modifiers,
        Key key,
        bool compositionSurfaceHasKeyboardFocus,
        bool editableControlHasKeyboardFocus,
        bool isTextCompositionActive)
    {
        if (!compositionSurfaceHasKeyboardFocus
            || editableControlHasKeyboardFocus
            || isTextCompositionActive
            || key != Key.G)
        {
            return SvgLayoutShortcutAction.None;
        }

        return modifiers switch
        {
            ModifierKeys.Control => SvgLayoutShortcutAction.Group,
            ModifierKeys.Control | ModifierKeys.Shift =>
                SvgLayoutShortcutAction.Ungroup,
            _ => SvgLayoutShortcutAction.None
        };
    }
}
