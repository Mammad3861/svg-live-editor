using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class FileDropSurfaceTests
{
    private static string ReadMainWindowXaml() =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "ui",
                "MainWindow.xaml"));

    [TestMethod]
    public void WindowLevelPreviewDrop_CoversEveryMajorPane()
    {
        string xaml = ReadMainWindowXaml();
        int windowDeclarationEnd = xaml.IndexOf('>');

        Assert.IsTrue(xaml.StartsWith("<Window ", StringComparison.Ordinal));
        Assert.IsTrue(windowDeclarationEnd > 0);
        string windowDeclaration = xaml[..(windowDeclarationEnd + 1)];
        StringAssert.Contains(windowDeclaration, "AllowDrop=\"True\"");
        StringAssert.Contains(
            windowDeclaration,
            "PreviewDragEnter=\"OnWindowPreviewDragEnter\"");
        StringAssert.Contains(
            windowDeclaration,
            "PreviewDragOver=\"OnWindowPreviewDragOver\"");
        StringAssert.Contains(
            windowDeclaration,
            "PreviewDragLeave=\"OnWindowPreviewDragLeave\"");
        StringAssert.Contains(
            windowDeclaration,
            "PreviewDrop=\"OnWindowDrop\"");

        StringAssert.Contains(xaml, "x:Name=\"InspectorWorkspace\"");
        StringAssert.Contains(xaml, "x:Name=\"InspectorModeTabs\"");
        StringAssert.Contains(xaml, "x:Name=\"LayersTab\"");
        StringAssert.Contains(xaml, "x:Name=\"StructureTab\"");
        StringAssert.Contains(xaml, "x:Name=\"InspectorPropertiesPanel\"");
        StringAssert.Contains(xaml, "x:Name=\"SourceEditor\"");
        StringAssert.Contains(xaml, "x:Name=\"PreviewWebView\"");

        int inspector = xaml.IndexOf(
            "x:Name=\"InspectorWorkspace\"",
            StringComparison.Ordinal);
        int modes = xaml.IndexOf(
            "x:Name=\"InspectorModeTabs\"",
            StringComparison.Ordinal);
        int layers = xaml.IndexOf(
            "x:Name=\"LayersTab\"",
            StringComparison.Ordinal);
        int structure = xaml.IndexOf(
            "x:Name=\"StructureTab\"",
            StringComparison.Ordinal);
        int properties = xaml.IndexOf(
            "x:Name=\"InspectorPropertiesPanel\"",
            StringComparison.Ordinal);
        int source = xaml.IndexOf(
            "x:Name=\"SourceEditor\"",
            StringComparison.Ordinal);
        int preview = xaml.IndexOf(
            "x:Name=\"PreviewWebView\"",
            StringComparison.Ordinal);

        Assert.IsTrue(modes > inspector);
        Assert.IsTrue(layers > modes);
        Assert.IsTrue(structure > layers);
        Assert.IsTrue(properties > structure);
        Assert.IsTrue(source > properties);
        Assert.IsTrue(preview > source);
    }

    [TestMethod]
    public void PreviewUsesCompositionControlAndRejectsBrowserDropHandling()
    {
        string xaml = ReadMainWindowXaml();

        StringAssert.Contains(
            xaml,
            "<wv2:WebView2CompositionControl");
        StringAssert.Contains(xaml, "AllowExternalDrop=\"False\"");
        Assert.IsFalse(xaml.Contains(
            "<wv2:WebView2 ",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void DropOverlay_IsInformationalAccessibleAndNonBlocking()
    {
        string xaml = ReadMainWindowXaml();

        StringAssert.Contains(xaml, "x:Name=\"FileDropOverlay\"");
        StringAssert.Contains(xaml, "Drop SVG or TXT to open");
        StringAssert.Contains(xaml, "IsHitTestVisible=\"False\"");
        StringAssert.Contains(xaml, "AutomationProperties.LiveSetting=\"Polite\"");
    }

    [TestMethod]
    public void OverlayState_CleansUpForAllTerminalEvents()
    {
        FileDropOverlayState state = new();
        Assert.IsTrue(state.Transition(
            FileDropOverlayEvent.SupportedDrag,
            "sample.svg").IsVisible);

        foreach (FileDropOverlayEvent terminalEvent in new[]
        {
            FileDropOverlayEvent.Drop,
            FileDropOverlayEvent.DragLeftWindow,
            FileDropOverlayEvent.Escape,
            FileDropOverlayEvent.WindowDeactivated,
            FileDropOverlayEvent.Cancelled
        })
        {
            state.Transition(
                FileDropOverlayEvent.SupportedDrag,
                "sample.svg");

            FileDropOverlayPresentation result =
                state.Transition(terminalEvent);

            Assert.IsFalse(
                result.IsVisible,
                $"Overlay remained visible after {terminalEvent}.");
            Assert.AreEqual(string.Empty, result.FileName);
        }
    }
}
