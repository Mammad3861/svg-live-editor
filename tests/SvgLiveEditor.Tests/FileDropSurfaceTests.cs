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

        StringAssert.Contains(xaml, "PreviewDragEnter=\"OnWindowPreviewDragEnter\"");
        StringAssert.Contains(xaml, "PreviewDragOver=\"OnWindowPreviewDragOver\"");
        StringAssert.Contains(xaml, "PreviewDrop=\"OnWindowDrop\"");
        StringAssert.Contains(xaml, "Document Inspector");
        StringAssert.Contains(xaml, "Properties");
        StringAssert.Contains(xaml, "x:Name=\"SourceEditor\"");
        StringAssert.Contains(xaml, "x:Name=\"PreviewWebView\"");
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
