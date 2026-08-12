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
        string applySelection = CompactCode(ExtractMethod(
            visual,
            "private void ApplyVisualSelectionState("));

        Assert.AreEqual(
            3,
            CountOccurrences(CompactCode(visual), "navigateSource:true"));
        StringAssert.Contains(
            applySelection,
            "boolnavigateSource=false)");
        StringAssert.Contains(
            applySelection,
            "_viewModel.Inspector.SelectNode(primaryNode,origin);");
        StringAssert.Contains(
            applySelection,
            "if(navigateSource&&_viewModel.Inspector.SelectedElementisSvgElementViewModelselected){NavigateToInspectorElement(selected,origin);}");
        Assert.AreEqual(
            1,
            CountOccurrences(
                applySelection,
                "NavigateToInspectorElement(selected,origin)"));
    }

    [TestMethod]
    public void CompositionShortcutsDoNotRouteThroughEditableOrSourceFocus()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string main = ReadUi("MainWindow.xaml.cs");
        string nativeInput = ReadUi("MainWindow.PreviewNativeInput.cs");
        string xaml = ReadUi("MainWindow.xaml");
        string windowRoute = CompactCode(ExtractMethod(
            main,
            "private void OnWindowPreviewKeyDown("));
        string previewRoute = CompactCode(ExtractMethod(
            main,
            "private void OnPreviewWebViewPreviewKeyDown("));
        string nudgeRoute = CompactCode(ExtractMethod(
            main,
            "private bool TryHandlePreviewNudgeShortcut("));
        string compositionRoute = CompactCode(ExtractMethod(
            inspector,
            "private bool TryHandleCompositionShortcut("));
        string modifierRoute = CompactCode(ExtractMethod(
            nativeInput,
            "private static System.Windows.Input.ModifierKeys"));
        string nativeKeyState = CompactCode(ExtractMethod(
            nativeInput,
            "private static bool IsNativeKeyDown("));

        StringAssert.Contains(
            compositionRoute,
            "SvgLayoutShortcutRouter.Resolve(");
        StringAssert.Contains(
            compositionRoute,
            "IsEditableControlFocused()");
        StringAssert.Contains(
            compositionRoute,
            "_isInspectorTextCompositionActive");
        StringAssert.Contains(
            compositionRoute,
            "HasPreviewKeyboardFocus()");
        StringAssert.Contains(
            windowRoute,
            "boolpreviewKeyRoute=ReferenceEquals(e.OriginalSource,PreviewWebView);");
        StringAssert.Contains(
            windowRoute,
            "TryHandlePreviewNudgeShortcut(modifiers,pressedKey,previewKeyRoute)");
        StringAssert.Contains(
            windowRoute,
            "TryHandleCompositionShortcut(modifiers,pressedKey,previewKeyRoute)");
        StringAssert.Contains(
            previewRoute,
            "TryHandlePreviewNudgeShortcut(modifiers,pressedKey,previewKeyRoute:true)");
        StringAssert.Contains(
            previewRoute,
            "TryHandleCompositionShortcut(modifiers,pressedKey,previewKeyRoute:true)");
        StringAssert.Contains(
            nudgeRoute,
            "boolpreviewHasKeyboardFocus=previewKeyRoute||HasPreviewKeyboardFocus();");
        StringAssert.Contains(
            nudgeRoute,
            "previewHasKeyboardFocus?false:SourceEditor.IsKeyboardFocusWithin");
        StringAssert.Contains(
            nudgeRoute,
            "previewHasKeyboardFocus?false:IsEditableControlFocused()");
        StringAssert.Contains(
            compositionRoute,
            "previewKeyRoute||HasPreviewKeyboardFocus()?false:IsEditableControlFocused()");
        StringAssert.Contains(main, "_isPreviewControllerKeyboardFocused = false;");
        StringAssert.Contains(
            modifierRoute,
            "GetPreviewAcceleratorModifiers(");
        StringAssert.Contains(nativeKeyState, "GetAsyncKeyState(virtualKey)");
        StringAssert.Contains(nativeKeyState, "GetKeyState(virtualKey)");
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
        string resolveSnap = CompactCode(ExtractMethod(
            visual,
            "private SvgSnapResult ResolveSnap("));

        StringAssert.Contains(
            resolveSnap,
            "ReferenceEquals(sourceDocument.FindParent(element.SourceElement),parent)");
        StringAssert.Contains(resolveSnap, "&&element.IsMovable");
        StringAssert.Contains(resolveSnap, "&&element.Geometryisnotnull");
        StringAssert.Contains(
            resolveSnap,
            "&&!_viewModel.Inspector.IsElementEffectivelyLocked(element.SourceElement)");
        StringAssert.Contains(
            resolveSnap,
            "&&_viewModel.Inspector.IsElementEffectivelyVisible(element.SourceElement)");
        StringAssert.Contains(
            resolveSnap,
            "return_objectSnapService.Snap(moving,siblings,document.Viewport,");
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

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Method signature not found: {signature}");
        int openingBrace = source.IndexOf('{', start);
        Assert.IsTrue(openingBrace >= 0, $"Method body not found: {signature}");
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[start..(index + 1)];
            }
        }

        Assert.Fail($"Method body was incomplete: {signature}");
        return string.Empty;
    }

    private static string CompactCode(string source) =>
        string.Concat(source.Where(character => !char.IsWhiteSpace(character)));
}
