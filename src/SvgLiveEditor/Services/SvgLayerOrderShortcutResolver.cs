using System.Windows.Input;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class SvgLayerOrderShortcutResolver
{
    public static SvgLayerOrderCommand? Resolve(
        ModifierKeys modifiers,
        Key key,
        bool isEditableControlFocused)
    {
        if (isEditableControlFocused)
        {
            return null;
        }

        return (modifiers, key) switch
        {
            (ModifierKeys.Control, Key.OemCloseBrackets) =>
                SvgLayerOrderCommand.BringForward,
            (ModifierKeys.Control, Key.OemOpenBrackets) =>
                SvgLayerOrderCommand.SendBackward,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.OemCloseBrackets) =>
                SvgLayerOrderCommand.BringToFront,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.OemOpenBrackets) =>
                SvgLayerOrderCommand.SendToBack,
            _ => null
        };
    }
}
