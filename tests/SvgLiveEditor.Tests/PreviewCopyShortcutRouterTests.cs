using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewCopyShortcutRouterTests
{
    [TestMethod]
    public void PreviewKeyboardFocusRoutesPlainCopyToPng()
    {
        CopyShortcutAction action = PreviewCopyShortcutRouter.Resolve(
            new CopyFocusState(
                PreviewHasKeyboardFocus: true,
                SourceEditorHasKeyboardFocus: false,
                TextFieldHasKeyboardFocus: false,
                PointerIsOverPreview: true));

        Assert.AreEqual(CopyShortcutAction.CopyPreviewAsPng, action);
    }

    [TestMethod]
    public void SourceEditorFocusLeavesSelectedTextCopyUntouched()
    {
        CopyShortcutAction action = PreviewCopyShortcutRouter.Resolve(
            new CopyFocusState(
                PreviewHasKeyboardFocus: false,
                SourceEditorHasKeyboardFocus: true,
                TextFieldHasKeyboardFocus: false,
                PointerIsOverPreview: true));

        Assert.AreEqual(CopyShortcutAction.LeaveNativeCopy, action);
    }

    [TestMethod]
    public void InspectorPropertyFocusLeavesSelectedFieldTextCopyUntouched()
    {
        CopyShortcutAction action = PreviewCopyShortcutRouter.Resolve(
            new CopyFocusState(
                PreviewHasKeyboardFocus: false,
                SourceEditorHasKeyboardFocus: false,
                TextFieldHasKeyboardFocus: true,
                PointerIsOverPreview: true));

        Assert.AreEqual(CopyShortcutAction.LeaveNativeCopy, action);
    }

    [TestMethod]
    public void PreviewHoverWithoutKeyboardFocusDoesNotRerouteCopy()
    {
        CopyShortcutAction action = PreviewCopyShortcutRouter.Resolve(
            new CopyFocusState(
                PreviewHasKeyboardFocus: false,
                SourceEditorHasKeyboardFocus: false,
                TextFieldHasKeyboardFocus: false,
                PointerIsOverPreview: true));

        Assert.AreEqual(CopyShortcutAction.Ignore, action);
    }

    [TestMethod]
    public void NativeTextFocusTakesPriorityOverPreviewFocus()
    {
        CopyShortcutAction action = PreviewCopyShortcutRouter.Resolve(
            new CopyFocusState(
                PreviewHasKeyboardFocus: true,
                SourceEditorHasKeyboardFocus: false,
                TextFieldHasKeyboardFocus: true,
                PointerIsOverPreview: true));

        Assert.AreEqual(CopyShortcutAction.LeaveNativeCopy, action);
    }
}
