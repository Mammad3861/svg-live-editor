using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualCompositionSurfaceTests
{
    [TestMethod]
    public void LayersAndArrangeExposeBoundedMultiSelectionCommands()
    {
        string xaml = ReadUi("MainWindow.xaml");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(xaml, "Binding IsMultiSelected");
        StringAssert.Contains(xaml, "Binding AutomationName");
        StringAssert.Contains(xaml, "x:Name=\"GroupMenuItem\"");
        StringAssert.Contains(xaml, "x:Name=\"UngroupMenuItem\"");
        StringAssert.Contains(xaml, "Tag=\"AlignLeft\"");
        StringAssert.Contains(xaml, "Tag=\"AlignHorizontalCenters\"");
        StringAssert.Contains(xaml, "Tag=\"AlignRight\"");
        StringAssert.Contains(xaml, "Tag=\"AlignTop\"");
        StringAssert.Contains(xaml, "Tag=\"AlignVerticalCenters\"");
        StringAssert.Contains(xaml, "Tag=\"AlignBottom\"");
        StringAssert.Contains(xaml, "Tag=\"DistributeHorizontally\"");
        StringAssert.Contains(xaml, "Tag=\"DistributeVertically\"");
        StringAssert.Contains(xaml, "x:Name=\"SnapToObjectsMenuItem\"");
        StringAssert.Contains(inspector, "HandleLayerMultiSelection(");
        StringAssert.Contains(inspector, "ModifierKeys.Control");
        StringAssert.Contains(inspector, "ModifierKeys.Shift");
        StringAssert.Contains(inspector, "layer.Parent.Children");
        StringAssert.Contains(
            inspector,
            "nodes.Length == state.Identities.Count");
        StringAssert.Contains(
            inspector,
            "elements.Length == state.Identities.Count");
    }

    [TestMethod]
    public void MultiMovementRemainsRevisionBoundAtomicAndSingleResizeOnly()
    {
        string visual = ReadUi("MainWindow.VisualEditing.cs");

        StringAssert.Contains(
            visual,
            "_visualSelectionState.SourceRevision == gesture.SourceRevision");
        StringAssert.Contains(
            visual,
            "gesture.SelectionIdentities");
        StringAssert.Contains(
            visual,
            "_multiVisualMoveService.CreateEdit(");
        StringAssert.Contains(
            visual,
            "_documentEditService.Apply(SourceEditor.Document, result.Edit)");
        StringAssert.Contains(
            visual,
            "_visualSelectionState.Identities.Count != 1");
        StringAssert.Contains(visual, "_activeSnapGuides = [];");
    }

    [TestMethod]
    public void ExplicitPreviewSelectionNavigatesSourceButRefreshDoesNot()
    {
        string visual = ReadUi("MainWindow.VisualEditing.cs");

        Assert.AreEqual(
            3,
            CountOccurrences(visual, "navigateSource: true"));
        StringAssert.Contains(
            visual,
            "if (navigateSource\n            && _viewModel.Inspector.SelectedElement");
        StringAssert.Contains(
            visual,
            "NavigateToInspectorElement(selected, origin)");
        Assert.AreEqual(
            1,
            CountOccurrences(
                visual,
                "NavigateToInspectorElement(selected, origin)"));
    }

    [TestMethod]
    public void CompositionShortcutsDoNotRouteThroughEditableOrSourceFocus()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string main = ReadUi("MainWindow.xaml.cs");
        string nativeInput = ReadUi("MainWindow.PreviewNativeInput.cs");
        string xaml = ReadUi("MainWindow.xaml");

        StringAssert.Contains(
            inspector,
            "SvgLayoutShortcutRouter.Resolve(");
        StringAssert.Contains(
            inspector,
            "IsEditableControlFocused()");
        StringAssert.Contains(
            inspector,
            "_isInspectorTextCompositionActive");
        StringAssert.Contains(
            inspector,
            "HasPreviewKeyboardFocus()");
        StringAssert.Contains(inspector, "previewKeyRoute");
        StringAssert.Contains(inspector, "|| IsActive");
        Assert.IsFalse(inspector.Contains(
            "SourceEditor.IsKeyboardFocusWithin\n            || compositionFocus",
            StringComparison.Ordinal));
        StringAssert.Contains(
            main,
            "ReferenceEquals(\n            e.OriginalSource,\n            PreviewWebView)");
        StringAssert.Contains(
            main,
            "TryHandlePreviewNudgeShortcut(\n                modifiers,\n                pressedKey,\n                previewKeyRoute)");
        StringAssert.Contains(
            main,
            "TryHandleCompositionShortcut(\n                modifiers,\n                pressedKey,\n                previewKeyRoute)");
        StringAssert.Contains(
            main,
            "OnPreviewWebViewPreviewKeyDown");
        StringAssert.Contains(main, "GetPreviewAcceleratorModifiers(");
        StringAssert.Contains(main, "_isPreviewControllerKeyboardFocused = false;");
        StringAssert.Contains(
            nativeInput,
            "GetPreviewAcceleratorModifiers(");
        StringAssert.Contains(nativeInput, "GetAsyncKeyState(virtualKey)");
        StringAssert.Contains(nativeInput, "GetKeyState(virtualKey)");
        StringAssert.Contains(xaml, "GotKeyboardFocus=\"OnWindowGotKeyboardFocus\"");
        StringAssert.Contains(xaml, "GotKeyboardFocus=\"OnPreviewWebViewGotKeyboardFocus\"");
        StringAssert.Contains(inspector, "GroupSelectedElements()");
        StringAssert.Contains(inspector, "UngroupSelectedGroup()");
        StringAssert.Contains(
            inspector,
            "result.ErrorMessage ?? \"The visual authoring operation was rejected.\"");
    }

    [TestMethod]
    public void TrustedPageKeepsPanPngAndMultiOverlayChannelsSeparate()
    {
        string html = new PreviewHtmlBuilder().Build(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"/>",
            300,
            150,
            "00112233445566778899AABBCCDDEEFF",
            PreviewViewportPosition.Center,
            1,
            new SvgVisualViewport(
                0,
                0,
                300,
                150,
                SvgPreserveAspectRatio.Default));

        StringAssert.Contains(html, "spaceHeld || panModeEnabled");
        StringAssert.Contains(
            html,
            "event.ctrlKey && !event.altKey && !event.shiftKey");
        StringAssert.Contains(html, "hasOutboundDragModifier(event)");
        StringAssert.Contains(html, "message.selections.length <= 128");
        StringAssert.Contains(html, "Object.keys(item).length === 10");
        StringAssert.Contains(html, "Object.keys(guide).length === 4");
        StringAssert.Contains(html, "context.drawImage(image, 0, 0");
        Assert.IsFalse(html.Contains(
            "drawImage(selectionOverlay",
            StringComparison.Ordinal));
        StringAssert.Contains(html, "stagedImage.decode()");
        StringAssert.Contains(html, "image.src = stagedImage.src");
    }

    [TestMethod]
    public void SnapCandidatesAreFilteredBeforeNearestCorrectionRanking()
    {
        string visual = ReadUi("MainWindow.VisualEditing.cs");

        StringAssert.Contains(visual, "&& element.IsMovable");
        StringAssert.Contains(
            visual,
            "IsElementEffectivelyLocked(\n                    element.SourceElement)");
        StringAssert.Contains(
            visual,
            "IsElementEffectivelyVisible(\n                    element.SourceElement)");
    }

    private static string ReadUi(string fileName)
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "ui", fileName));
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
