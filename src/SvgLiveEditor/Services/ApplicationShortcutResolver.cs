using System.Windows.Input;

namespace SvgLiveEditor.Services;

public enum ApplicationShortcut
{
    ToggleWordWrap
}

public static class ApplicationShortcutResolver
{
    public static ApplicationShortcut? Resolve(ModifierKeys modifiers, Key key)
    {
        bool isAltZ = modifiers == ModifierKeys.Alt && key == Key.Z;
        bool isControlAltW = modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && key == Key.W;
        return isAltZ || isControlAltW
            ? ApplicationShortcut.ToggleWordWrap
            : null;
    }
}
