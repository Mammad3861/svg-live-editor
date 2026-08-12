using System.Windows.Input;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class PreviewVisualNudgeFocusPolicy
{
    public static bool CanRoute(
        bool previewHasKeyboardFocus,
        bool sourceEditorHasKeyboardFocus,
        bool propertyFieldHasKeyboardFocus)
    {
        return previewHasKeyboardFocus
            && !sourceEditorHasKeyboardFocus
            && !propertyFieldHasKeyboardFocus;
    }

    public static bool TryResolveShortcut(
        ModifierKeys modifiers,
        Key key,
        bool previewHasKeyboardFocus,
        bool sourceEditorHasKeyboardFocus,
        bool editableControlHasKeyboardFocus,
        bool isPanModeEnabled,
        bool isTextCompositionActive,
        long sourceRevision,
        out PreviewVisualNudgeRequest request)
    {
        request = default;
        if (!CanRoute(
                previewHasKeyboardFocus,
                sourceEditorHasKeyboardFocus,
                editableControlHasKeyboardFocus)
            || isPanModeEnabled
            || isTextCompositionActive
            || modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
        {
            return false;
        }

        double step = modifiers == ModifierKeys.Shift ? 10 : 1;
        (double DeltaX, double DeltaY)? delta = key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => null
        };
        if (delta is not { } value)
        {
            return false;
        }

        request = new PreviewVisualNudgeRequest(
            sourceRevision,
            value.DeltaX,
            value.DeltaY);
        return true;
    }
}
