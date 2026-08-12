using System.Windows.Input;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayoutShortcutRouterTests
{
    [TestMethod]
    public void GroupAndUngroupRequireCompositionFocus()
    {
        Assert.AreEqual(
            SvgLayoutShortcutAction.Group,
            Resolve(ModifierKeys.Control));
        Assert.AreEqual(
            SvgLayoutShortcutAction.Ungroup,
            Resolve(ModifierKeys.Control | ModifierKeys.Shift));
        Assert.AreEqual(
            SvgLayoutShortcutAction.None,
            SvgLayoutShortcutRouter.Resolve(
                ModifierKeys.Control,
                Key.G,
                compositionSurfaceHasKeyboardFocus: false,
                editableControlHasKeyboardFocus: false,
                isTextCompositionActive: false));
    }

    [TestMethod]
    public void EditableControlsAndImeCompositionKeepCtrlGForTextEditing()
    {
        Assert.AreEqual(
            SvgLayoutShortcutAction.None,
            SvgLayoutShortcutRouter.Resolve(
                ModifierKeys.Control,
                Key.G,
                compositionSurfaceHasKeyboardFocus: true,
                editableControlHasKeyboardFocus: true,
                isTextCompositionActive: false));
        Assert.AreEqual(
            SvgLayoutShortcutAction.None,
            SvgLayoutShortcutRouter.Resolve(
                ModifierKeys.Control | ModifierKeys.Shift,
                Key.G,
                compositionSurfaceHasKeyboardFocus: true,
                editableControlHasKeyboardFocus: false,
                isTextCompositionActive: true));
    }

    [TestMethod]
    public void OtherModifiersAndKeysDoNotRoute()
    {
        Assert.AreEqual(
            SvgLayoutShortcutAction.None,
            Resolve(ModifierKeys.Control | ModifierKeys.Alt));
        Assert.AreEqual(
            SvgLayoutShortcutAction.None,
            SvgLayoutShortcutRouter.Resolve(
                ModifierKeys.Control,
                Key.C,
                compositionSurfaceHasKeyboardFocus: true,
                editableControlHasKeyboardFocus: false,
                isTextCompositionActive: false));
    }

    private static SvgLayoutShortcutAction Resolve(ModifierKeys modifiers) =>
        SvgLayoutShortcutRouter.Resolve(
            modifiers,
            Key.G,
            compositionSurfaceHasKeyboardFocus: true,
            editableControlHasKeyboardFocus: false,
            isTextCompositionActive: false);
}
