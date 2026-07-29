namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class NativePreviewInputSurfaceTests
{
    [TestMethod]
    public void MainWindowHookIsWindowLocalPreviewScopedAndRemovedOnClose()
    {
        string nativeInput = ReadUi("MainWindow.PreviewNativeInput.cs");
        string main = ReadUi("MainWindow.xaml.cs");

        StringAssert.Contains(nativeInput, "protected override void OnSourceInitialized");
        StringAssert.Contains(nativeInput, "source.AddHook(OnMainWindowMessage)");
        StringAssert.Contains(nativeInput, "TryIsPointerOverReadyPreview");
        StringAssert.Contains(
            nativeInput,
            "_nativeHorizontalWheelMessageParser.TryParse(");
        StringAssert.Contains(
            nativeInput,
            "_previewNavigationPolicy.IsTrustedWebMessageSource(core.Source)");
        StringAssert.Contains(nativeInput, "_previewContextMenu.IsOpen");
        StringAssert.Contains(
            nativeInput,
            "FileDropOverlay.Visibility == Visibility.Visible");
        StringAssert.Contains(nativeInput, "core.PostWebMessageAsJson(");
        StringAssert.Contains(nativeInput, "handled = true;");
        StringAssert.Contains(nativeInput, "source.RemoveHook(OnMainWindowMessage)");
        StringAssert.Contains(main, "DetachNativePreviewInputHook();");
        StringAssert.Contains(main, "_isWindowClosing = true;");
        StringAssert.Contains(
            nativeInput,
            "return PreviewNativeInputState.Disposed;");
        Assert.IsFalse(nativeInput.Contains(
            "SetWindowsHookEx",
            StringComparison.Ordinal));
        Assert.IsFalse(nativeInput.Contains(
            "WM_GESTURE",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void NativeMessageIsMarkedHandledOnlyAfterValidatedPageForward()
    {
        string nativeInput = ReadUi("MainWindow.PreviewNativeInput.cs");
        int policy = nativeInput.IndexOf(
            "_previewNativeHorizontalScrollPolicy.TryCreateRequest",
            StringComparison.Ordinal);
        int post = nativeInput.IndexOf(
            "core.PostWebMessageAsJson(",
            policy,
            StringComparison.Ordinal);
        int handled = nativeInput.IndexOf(
            "handled = true;",
            post,
            StringComparison.Ordinal);

        Assert.IsTrue(policy >= 0);
        Assert.IsTrue(post > policy);
        Assert.IsTrue(handled > post);
        Assert.IsFalse(nativeInput.Contains(
            "Task.Delay",
            StringComparison.Ordinal));
        Assert.IsFalse(nativeInput.Contains(
            "DispatcherTimer",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void TemporaryDiagnosticsAndGeneralCommandChannelsAreAbsent()
    {
        string nativeInput = ReadUi("MainWindow.PreviewNativeInput.cs");
        string main = ReadUi("MainWindow.xaml.cs");

        Assert.IsFalse(nativeInput.Contains(
            "Debug.Write",
            StringComparison.Ordinal));
        Assert.IsFalse(nativeInput.Contains(
            "inputDiagnostic",
            StringComparison.Ordinal));
        Assert.IsFalse(main.Contains(
            "inputDiagnostic",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainEditorKeepsInspectorSourceAndPreviewVisibleInOrder()
    {
        string xaml = ReadUi("MainWindow.xaml");
        int inspector = xaml.IndexOf(
            "Text=\"Document Inspector\"",
            StringComparison.Ordinal);
        int source = xaml.IndexOf(
            "Text=\"Source\"",
            StringComparison.Ordinal);
        int preview = xaml.IndexOf(
            "Text=\"Live Preview\"",
            StringComparison.Ordinal);

        Assert.IsTrue(inspector >= 0);
        Assert.IsTrue(source > inspector);
        Assert.IsTrue(preview > source);
        StringAssert.Contains(xaml, "MinWidth=\"1020\"");
        StringAssert.Contains(
            xaml,
            "<ColumnDefinition Width=\"270\" MinWidth=\"220\" MaxWidth=\"460\" />");
        Assert.AreEqual(
            2,
            CountOccurrences(xaml, "<ColumnDefinition Width=\"*\" MinWidth=\"320\" />"));
        Assert.AreEqual(
            2,
            CountOccurrences(xaml, "<GridSplitter Grid.Column="));
    }

    [TestMethod]
    public void TemplateAndRecoveryDialogsRemainOwnedModalOverlays()
    {
        string persistence = ReadUi("MainWindow.Persistence.cs");

        Assert.AreEqual(
            2,
            CountOccurrences(persistence, "Owner = this"));
        Assert.IsTrue(
            CountOccurrences(persistence, "ShowDialog()") >= 2);
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int position = 0;
        while ((position = value.IndexOf(
                   search,
                   position,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += search.Length;
        }
        return count;
    }

    private static string ReadUi(string fileName)
    {
        return File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "ui",
                fileName));
    }
}
