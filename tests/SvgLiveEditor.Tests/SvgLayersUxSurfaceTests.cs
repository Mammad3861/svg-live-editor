namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayersUxSurfaceTests
{
    [TestMethod]
    public void LayersAndStructureAreSeparateAccessibleModes()
    {
        string xaml = ReadUi("MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"InspectorWorkspace\"");
        StringAssert.Contains(xaml, "x:Name=\"LayersTab\"");
        StringAssert.Contains(xaml, "Header=\"Layers\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Layers\"");
        StringAssert.Contains(xaml, "x:Name=\"StructureTab\"");
        StringAssert.Contains(xaml, "Header=\"Structure\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"SVG XML Structure\"");
        StringAssert.Contains(xaml, "x:Name=\"LayersTree\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding Inspector.LayerRoots}\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Layers, frontmost first\"");
        StringAssert.Contains(xaml, "AutomationProperties.HelpText=\"Items higher in this list paint in front");
    }

    [TestMethod]
    public void LayerRowsExposeOnlyAppOwnedVisibilityLockAndTypeControls()
    {
        string xaml = ReadUi("MainWindow.xaml");

        StringAssert.Contains(xaml, "Click=\"OnLayerVisibilityClick\"");
        StringAssert.Contains(xaml, "Click=\"OnLayerLockClick\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"{Binding VisibilityAutomationName}\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"{Binding LockAutomationName}\"");
        StringAssert.Contains(xaml, "AutomationProperties.HelpText=\"{Binding VisibilityHelp}\"");
        StringAssert.Contains(xaml, "AutomationProperties.HelpText=\"{Binding LockHelp}\"");
        StringAssert.Contains(xaml, "AutomationProperties.HelpText\" Value=\"{Binding RowHelp}\"");
        StringAssert.Contains(xaml, "Visibility=\"{Binding IsInspectionOnly");
        StringAssert.Contains(xaml, "M1,8 C3.2,4.4");
        StringAssert.Contains(xaml, "M3,7.5 H13 V15 H3 Z");
        StringAssert.Contains(xaml, "SystemColors.ControlTextBrushKey");
        StringAssert.Contains(xaml, "IsKeyboardFocused");
        Assert.IsFalse(xaml.Contains("Content=\"{Binding VisibilityGlyph}\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Content=\"{Binding LockGlyph}\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("ContextMenuService", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SelectionTabSwitchingAndSessionLocksDoNotNavigatePreview()
    {
        string xaml = ReadUi("MainWindow.xaml");
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string selection = ExtractSection(
            inspector,
            "private void OnLayersTreeSelectionChanged(",
            "private void OnLayersTreePreviewKeyDown(");
        string sessionLock = ExtractSection(
            inspector,
            "private void OnLayerLockClick(",
            "private void ApplyLayerVisibility(");

        StringAssert.Contains(xaml, "x:Name=\"InspectorModeTabs\"");
        Assert.IsFalse(xaml.Contains(
            "SelectionChanged=\"OnInspectorMode",
            StringComparison.Ordinal));
        AssertHasNoPreviewNavigation(selection);
        AssertHasNoPreviewNavigation(sessionLock);
        StringAssert.Contains(sessionLock, "ToggleLayerLock(layer)");
    }

    [TestMethod]
    public void DragDropUsesOpaqueIdsAndHostSideSameParentPolicy()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(inspector, "SvgLiveEditor.Internal.Layer.OpaqueId");
        StringAssert.Contains(inspector, "_sourceRevisionTracker.IsCurrent");
        StringAssert.Contains(inspector, "document.FindParent(source.Element)");
        StringAssert.Contains(inspector, "Moving into or out of a group is deferred to v0.9");
        Assert.IsFalse(inspector.Contains("PostWebMessage", StringComparison.Ordinal));
        string main = ReadUi("MainWindow.xaml.cs");
        StringAssert.Contains(
            main,
            "if (e.Data.GetDataPresent(LayerDragDataFormat))");
        StringAssert.Contains(main, "e.Handled = false;");
    }

    [TestMethod]
    public void LayerOpacityAndPropertiesRemainPinnedAboveAttributeScroller()
    {
        string xaml = ReadUi("MainWindow.xaml");
        int layer = xaml.IndexOf(
            "AutomationProperties.Name=\"Current SVG layer position\"",
            StringComparison.Ordinal);
        int opacity = xaml.IndexOf(
            "x:Name=\"OpacitySlider\"",
            StringComparison.Ordinal);
        int properties = xaml.IndexOf(
            "<ScrollViewer VerticalScrollBarVisibility=\"Auto\"",
            opacity,
            StringComparison.Ordinal);

        Assert.IsTrue(layer >= 0);
        Assert.IsTrue(opacity > layer);
        Assert.IsTrue(properties > opacity);
        StringAssert.Contains(xaml, "<RowDefinition Height=\"*\" />");
    }

    [TestMethod]
    public void SessionLocksGuardVisualArrangeDragAndPropertyMutations()
    {
        string visual = ReadUi("MainWindow.VisualEditing.cs");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(visual, "IsVisualElementLocked(element)");
        StringAssert.Contains(visual, "IsVisualElementLocked(selectedElement)");
        StringAssert.Contains(inspector, "IsElementEffectivelyLocked(source.Element)");
        StringAssert.Contains(inspector, "IsElementEffectivelyLocked(target.Element)");
        StringAssert.Contains(inspector, "IsElementEffectivelyLocked(opacity.Element)");
        StringAssert.Contains(inspector, "IsElementEffectivelyLocked(property.Element)");
        StringAssert.Contains(inspector, "IsElementEffectivelyLocked(element)");
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

    private static void AssertHasNoPreviewNavigation(string source)
    {
        foreach (string marker in new[]
        {
            "QueuePreviewUpdate",
            "RefreshPreviewNow",
            "ShowLastValidPreview",
            "NavigateToString"
        })
        {
            Assert.IsFalse(
                source.Contains(marker, StringComparison.Ordinal),
                $"Selection-only interaction unexpectedly referenced {marker}.");
        }
    }
}
