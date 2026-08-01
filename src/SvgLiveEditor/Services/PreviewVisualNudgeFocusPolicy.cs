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
}
