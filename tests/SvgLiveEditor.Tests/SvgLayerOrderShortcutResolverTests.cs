using System.Windows.Input;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayerOrderShortcutResolverTests
{
    [TestMethod]
    public void ResolvesConventionalArrangeShortcuts()
    {
        Assert.AreEqual(
            SvgLayerOrderCommand.BringForward,
            Resolve(ModifierKeys.Control, Key.OemCloseBrackets));
        Assert.AreEqual(
            SvgLayerOrderCommand.SendBackward,
            Resolve(ModifierKeys.Control, Key.OemOpenBrackets));
        Assert.AreEqual(
            SvgLayerOrderCommand.BringToFront,
            Resolve(
                ModifierKeys.Control | ModifierKeys.Shift,
                Key.OemCloseBrackets));
        Assert.AreEqual(
            SvgLayerOrderCommand.SendToBack,
            Resolve(
                ModifierKeys.Control | ModifierKeys.Shift,
                Key.OemOpenBrackets));
    }

    [TestMethod]
    public void EditableControlFocusNeverRoutesArrangeShortcut()
    {
        Assert.IsNull(SvgLayerOrderShortcutResolver.Resolve(
            ModifierKeys.Control,
            Key.OemCloseBrackets,
            isEditableControlFocused: true));
        Assert.IsNull(SvgLayerOrderShortcutResolver.Resolve(
            ModifierKeys.Control | ModifierKeys.Shift,
            Key.OemOpenBrackets,
            isEditableControlFocused: true));
    }

    private static SvgLayerOrderCommand? Resolve(
        ModifierKeys modifiers,
        Key key) =>
        SvgLayerOrderShortcutResolver.Resolve(
            modifiers,
            key,
            isEditableControlFocused: false);
}
