namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualAuthoringUxSurfaceTests
{
    [TestMethod]
    public void AddControlAndBothTreesExposeOnlyKnownAuthoringCommands()
    {
        string xaml = ReadUi("MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"AddElementButton\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Add\"");
        StringAssert.Contains(xaml, "Data=\"M6,1 L6,11 M1,6 L11,6\"");
        StringAssert.Contains(xaml, "Width=\"12\" Height=\"12\"");
        StringAssert.Contains(xaml, "SystemColors.ControlTextBrushKey");
        StringAssert.Contains(xaml, "SnapsToDevicePixels=\"True\"");
        Assert.IsFalse(xaml.Contains("Content=\"+\"", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Layers, frontmost first\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"SVG element tree\"");
        Assert.IsTrue(
            xaml.Split(
                "AutomationProperties.Name\" Value=\"{Binding Label}\"",
                StringSplitOptions.None).Length >= 3);
        StringAssert.Contains(xaml, "ContextMenu=\"{StaticResource InspectorAuthoringContextMenu}\"");
        foreach (string tag in new[]
        {
            "Create:Root:Rectangle",
            "Create:Root:Circle",
            "Create:Root:Ellipse",
            "Create:Root:Line",
            "Create:Root:Text",
            "Create:Root:Group",
            "Create:Context:Rectangle",
            "Create:Context:Group",
            "Duplicate",
            "Delete",
            "Rename",
            "MoveToRoot"
        })
        {
            StringAssert.Contains(xaml, $"Tag=\"{tag}\"");
        }
        Assert.IsFalse(xaml.Contains("Create:Path", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "PreviewMouseRightButtonDown=\"OnInspectorTreePreviewMouseRightButtonDown\"");
        StringAssert.Contains(xaml, "AutomationProperties.HelpText=\"Choose the SVG root or selected safe layer context");
    }

    [TestMethod]
    public void KeyboardCommandsAreFocusScopedAndUndoUsesExistingDocumentRoute()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string main = ReadUi("MainWindow.xaml.cs");
        string shortcut = ExtractSection(
            inspector,
            "private bool TryHandleAuthoringShortcut(",
            "private void OnInspectorTreePreviewMouseRightButtonDown(");

        StringAssert.Contains(shortcut, "LayersTree.IsKeyboardFocusWithin");
        StringAssert.Contains(shortcut, "InspectorTree.IsKeyboardFocusWithin");
        StringAssert.Contains(shortcut, "InspectorPropertiesPanel.IsKeyboardFocusWithin");
        StringAssert.Contains(shortcut, "PreviewWebView.IsKeyboardFocusWithin");
        StringAssert.Contains(shortcut, "IsEditableControlFocused()");
        StringAssert.Contains(shortcut, "SvgAuthoringShortcutRouter.Resolve");
        StringAssert.Contains(shortcut, "SvgAuthoringShortcutAction.Duplicate");
        StringAssert.Contains(shortcut, "SvgAuthoringShortcutAction.Delete");
        StringAssert.Contains(main, "TryHandleAuthoringShortcut(modifiers, pressedKey)");
        StringAssert.Contains(main, "TryParseAuthoringCommand(");
        StringAssert.Contains(main, "_viewModel.Inspector.SelectedElement is null");
        StringAssert.Contains(inspector, "_documentEditService.Apply(SourceEditor.Document, result.Edit)");
        StringAssert.Contains(inspector, "TryHandleInspectorUndoShortcut");
    }

    [TestMethod]
    public void DragFeedbackDistinguishesBeforeAfterAndInsideGroup()
    {
        string xaml = ReadUi("MainWindow.xaml");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(xaml, "Visibility=\"{Binding IsDropBefore");
        StringAssert.Contains(xaml, "Visibility=\"{Binding IsDropAfter");
        StringAssert.Contains(xaml, "Visibility=\"{Binding IsDropInside");
        StringAssert.Contains(xaml, "center of a group to move inside it");
        StringAssert.Contains(inspector, "position.Y >= height * 0.3");
        StringAssert.Contains(inspector, "SvgLayerDropPlacement.Inside");
        StringAssert.Contains(inspector, "SetLayerDropTarget(target, placement)");
    }

    [TestMethod]
    public void AuthoringUsesNormalValidatedLatestPreviewPipelineWithoutSourceSelectionTheft()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string apply = ExtractSection(
            inspector,
            "private bool ApplyAuthoringEdit(",
            "private bool TryGetAuthoringContext(");

        StringAssert.Contains(apply, "_sourceRevisionTracker.IsCurrent(expectedRevision)");
        StringAssert.Contains(apply, "_previewDebouncer.Cancel()");
        StringAssert.Contains(apply, "ApplyValidationResult(");
        StringAssert.Contains(apply, "result.PreferredSelection");
        Assert.IsFalse(apply.Contains("NavigateToString", StringComparison.Ordinal));
        Assert.IsFalse(apply.Contains("SourceEditor.Select", StringComparison.Ordinal));
        Assert.IsFalse(apply.Contains("PostWebMessage", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DeleteConfirmationAndDisabledReasonsAreAppOwnedAndAccessible()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(inspector, "result.RequiresConfirmation");
        StringAssert.Contains(inspector, "MessageBoxButton.YesNo");
        StringAssert.Contains(inspector, "MessageBoxResult.No");
        StringAssert.Contains(inspector, "item.IsEnabled = availability.CanExecute");
        StringAssert.Contains(inspector, "AutomationProperties.SetHelpText");
        StringAssert.Contains(inspector, "AddElementButton.ToolTip = help");
    }

    private static string ReadUi(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "ui", fileName));

    private static string ExtractSection(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.IsTrue(end > start);
        return source[start..end];
    }
}
