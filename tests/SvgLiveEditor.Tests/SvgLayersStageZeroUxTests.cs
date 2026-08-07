using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayersStageZeroUxTests
{
    [TestMethod]
    public void GroupDisclosureStateIsSourceNeutralAndAccessible()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"parent\"><g id=\"child\"><rect id=\"item\"/></g></g><circle id=\"leaf\"/></svg>";
        SvgDocumentIndex document = new SvgDocumentIndexService()
            .Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);
        SvgLayerViewModel parent = inspector.LayerRoots.Single(layer =>
            layer.Element.Id == "parent");
        SvgLayerViewModel leaf = inspector.LayerRoots.Single(layer =>
            layer.Element.Id == "leaf");

        Assert.IsTrue(parent.HasExpandableChildren);
        SourceSpan sourceSpan = parent.Element.FullSpan;
        StringAssert.StartsWith(parent.ExpansionAutomationName, "Collapse");
        parent.IsExpanded = false;
        StringAssert.StartsWith(parent.ExpansionAutomationName, "Expand");
        Assert.IsFalse(leaf.HasExpandableChildren);
        Assert.AreEqual(sourceSpan, parent.Element.FullSpan);
        Assert.AreEqual(
            "<g id=\"parent\"><g id=\"child\"><rect id=\"item\"/></g></g>",
            source.Substring(sourceSpan.Start, sourceSpan.Length));
    }

    [TestMethod]
    public void LayersXamlUsesExplicitNativeDisclosureAndInlineRenameSurfaces()
    {
        string xaml = ReadUi("MainWindow.xaml");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(xaml, "x:Key=\"LayerDisclosureToggleStyle\"");
        StringAssert.Contains(xaml, "ItemContainerStyle=\"{StaticResource LayerTreeItemStyle}\"");
        StringAssert.Contains(xaml, "IsChecked=\"{Binding IsExpanded, Mode=TwoWay}\"");
        StringAssert.Contains(xaml, "Visibility=\"{Binding HasExpandableChildren");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"{Binding ExpansionAutomationName}\"");
        StringAssert.Contains(xaml, "SystemColors.ControlTextBrushKey");
        StringAssert.Contains(xaml, "Margin=\"18,0,0,0\"");
        StringAssert.Contains(xaml, "InputGestureText=\"F2\"");
        StringAssert.Contains(xaml, "OnLayerRenameTextBoxKeyDown");
        StringAssert.Contains(xaml, "Enter or focus loss saves; Escape cancels");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Friendly layer name\"");
        StringAssert.Contains(xaml, "ToolTip=\"{Binding TechnicalLabel}\"");
        StringAssert.Contains(
            inspector,
            "InspectorModeTabs.SelectedItem = LayersTab;");
        StringAssert.Contains(inspector, "cancelOnFailure: true");
        StringAssert.Contains(inspector, "layer.EndRename();");
        Assert.IsFalse(xaml.Contains("Content=\"•\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LayerRenameEnterIsPreviewRoutedBeforeTreeNavigationAndDeduplicated()
    {
        string inspector = ReadUi("MainWindow.Inspector.cs");
        string treeKeyRoute = ExtractSection(
            inspector,
            "private void OnLayersTreePreviewKeyDown(",
            "private void OnLayersTreePreviewMouseLeftButtonDown(");
        string renameKeyRoute = ExtractSection(
            inspector,
            "private bool TryHandleLayerRenameKey(",
            "private void OnLayerRenameTextBoxLostKeyboardFocus(");
        string lostFocus = ExtractSection(
            inspector,
            "private void OnLayerRenameTextBoxLostKeyboardFocus(",
            "private void CommitLayerRename(");
        string commit = ExtractSection(
            inspector,
            "private void CommitLayerRename(",
            "private void QueueFocusSelectedLayerRow(");

        int renamePreviewRoute = treeKeyRoute.IndexOf(
            "TryHandleLayerRenameKey(renameTextBox, renameLayer, key)",
            StringComparison.Ordinal);
        int ordinaryTreeEnter = treeKeyRoute.IndexOf(
            "if (key is Key.Enter or Key.Space)",
            StringComparison.Ordinal);
        Assert.IsTrue(renamePreviewRoute >= 0);
        Assert.IsTrue(ordinaryTreeEnter > renamePreviewRoute);
        StringAssert.Contains(renameKeyRoute, "key != Key.Enter");
        StringAssert.Contains(renameKeyRoute, "restoreLayerFocus: true");
        StringAssert.Contains(renameKeyRoute, "QueueFocusSelectedLayerRow();");
        StringAssert.Contains(lostFocus, "IsRenaming: true");
        StringAssert.Contains(lostFocus, "restoreLayerFocus: false");
        Assert.IsTrue(
            commit.IndexOf("layer.EndRename();", StringComparison.Ordinal)
            < commit.IndexOf("ApplyAuthoringEdit(", StringComparison.Ordinal));
        StringAssert.Contains(commit, "QueueFocusSelectedLayerRow();");
    }

    [TestMethod]
    public void CreationMenuMakesRootAndSelectedContextExplicit()
    {
        string xaml = ReadUi("MainWindow.xaml");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(xaml, "Tag=\"CreateDestination:Root\"");
        StringAssert.Contains(xaml, "Tag=\"CreateDestination:Context\"");
        StringAssert.Contains(xaml, "Tag=\"Create:Root:Group\"");
        StringAssert.Contains(xaml, "Tag=\"Create:Context:Group\"");
        StringAssert.Contains(inspector, "SvgCreateDestination.SvgRoot");
        StringAssert.Contains(inspector, "SvgCreateDestination.SelectedContext");
        StringAssert.Contains(inspector, "created in {destinationLabel}");
        StringAssert.Contains(xaml, "HorizontalContentAlignment=\"Center\"");
        StringAssert.Contains(xaml, "VerticalContentAlignment=\"Center\"");
    }

    [TestMethod]
    public void PreviewStatePanelNeverUnmountsWebViewCompositionSurface()
    {
        string main = ReadUi("MainWindow.xaml.cs");
        string loading = ExtractSection(
            main,
            "private void ShowPreviewLoading(",
            "private void ShowPreviewRefreshing(");
        string error = ExtractSection(
            main,
            "private void ShowPreviewError(",
            "private void UpdatePreviewStateText(");

        StringAssert.Contains(loading, "PreviewWebView.Visibility = Visibility.Visible");
        StringAssert.Contains(error, "PreviewWebView.Visibility = Visibility.Visible");
        Assert.IsFalse(loading.Contains("Visibility.Hidden", StringComparison.Ordinal));
        Assert.IsFalse(error.Contains("Visibility.Hidden", StringComparison.Ordinal));

        string completion = ExtractSection(
            main,
            "private void CompleteActivePreviewRender(",
            "private void StartPreviewRenderTimeout(");
        Assert.IsTrue(
            completion.IndexOf(
                "_previewNavigationCoordinator.TryComplete(",
                StringComparison.Ordinal)
            < completion.IndexOf(
                "_previewRenderReadiness.Reset();",
                StringComparison.Ordinal));
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
