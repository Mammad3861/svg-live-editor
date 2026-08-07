using System.Windows.Input;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgAuthoringShortcutRouterTests
{
    [TestMethod]
    public void PreviewLayersAndStructureRouteDeleteAndBackspace()
    {
        Assert.AreEqual(
            SvgAuthoringShortcutAction.Delete,
            Resolve(Key.Delete));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.Delete,
            Resolve(Key.Back));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.Duplicate,
            Resolve(Key.D, ModifierKeys.Control));
    }

    [TestMethod]
    public void EditableControlsAndImeCompositionKeepDeletionLocal()
    {
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.Delete, editable: true));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.Back, editable: true));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.Delete, composing: true));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            SvgAuthoringShortcutRouter.Resolve(
                ModifierKeys.None,
                Key.Delete,
                authoringSurfaceHasKeyboardFocus: false,
                editableControlHasKeyboardFocus: false,
                isTextCompositionActive: false));
    }

    [TestMethod]
    [DataRow("Preview", true, false, false, true)]
    [DataRow("Layers row", true, false, false, true)]
    [DataRow("Structure row", true, false, false, true)]
    [DataRow("Properties non-editing surface", true, false, false, true)]
    [DataRow("Source editor", false, true, false, false)]
    [DataRow("Property TextBox", true, true, false, false)]
    [DataRow("Editable ComboBox", true, true, false, false)]
    [DataRow("Font picker", true, true, false, false)]
    [DataRow("Rename editor", true, true, false, false)]
    [DataRow("IME composition", true, false, true, false)]
    public void DeleteFocusMatrixKeepsEditingLocal(
        string focusClass,
        bool authoringSurface,
        bool editable,
        bool composing,
        bool expectedDelete)
    {
        SvgAuthoringShortcutAction action =
            SvgAuthoringShortcutRouter.Resolve(
                ModifierKeys.None,
                Key.Delete,
                authoringSurface,
                editable,
                composing);

        Assert.AreEqual(
            expectedDelete
                ? SvgAuthoringShortcutAction.Delete
                : SvgAuthoringShortcutAction.None,
            action,
            focusClass);
    }

    [TestMethod]
    public void ModifiedDeleteAndUnrelatedShortcutsAreNotRerouted()
    {
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.Delete, ModifierKeys.Control));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.C, ModifierKeys.Control));
        Assert.AreEqual(
            SvgAuthoringShortcutAction.None,
            Resolve(Key.Z, ModifierKeys.Control));
    }

    private static SvgAuthoringShortcutAction Resolve(
        Key key,
        ModifierKeys modifiers = ModifierKeys.None,
        bool editable = false,
        bool composing = false) =>
        SvgAuthoringShortcutRouter.Resolve(
            modifiers,
            key,
            authoringSurfaceHasKeyboardFocus: true,
            editable,
            composing);
}
