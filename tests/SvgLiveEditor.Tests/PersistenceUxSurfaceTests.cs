namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PersistenceUxSurfaceTests
{
    [TestMethod]
    public void MainWindowExposesTemplatesAndPersistedAutoSave()
    {
        string xaml = ReadUi("MainWindow.xaml");

        StringAssert.Contains(xaml, "New from _Template...");
        StringAssert.Contains(xaml, "Ctrl+Alt+N");
        StringAssert.Contains(xaml, "Content=\"Templates\"");
        StringAssert.Contains(xaml, "x:Name=\"AutoSaveMenuItem\"");
        StringAssert.Contains(xaml, "IsCheckable=\"True\"");
    }

    [TestMethod]
    public void GalleriesAreNativeWpfAndKeyboardAccessible()
    {
        string templates = ReadUi("TemplateGalleryWindow.xaml");
        string recovery = ReadUi("RecoveryWindow.xaml");

        Assert.IsFalse(templates.Contains(
            "WebView",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(recovery.Contains(
            "WebView",
            StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(templates, "IsDefault=\"True\"");
        StringAssert.Contains(templates, "IsCancel=\"True\"");
        StringAssert.Contains(
            templates,
            "AutomationProperties.Name\" Value=\"{Binding Name}\"");
        StringAssert.Contains(recovery, "Content=\"Restore\"");
        StringAssert.Contains(recovery, "Content=\"Discard\"");
        StringAssert.Contains(recovery, "Content=\"Skip\"");
        StringAssert.Contains(recovery, "IsDefault=\"True\"");
        StringAssert.Contains(recovery, "IsCancel=\"True\"");
        StringAssert.Contains(
            recovery,
            "AutomationProperties.Name\" Value=\"{Binding Snapshot.DisplayName}\"");
    }

    [TestMethod]
    public void StartupChecksRecoveryBeforeLastDocument()
    {
        string main = ReadUi("MainWindow.xaml.cs");
        int recovery = main.IndexOf(
            "TryRestoreRecoverySnapshot()",
            StringComparison.Ordinal);
        int lastDocument = main.IndexOf(
            "_lastDocumentService.TryRestore",
            StringComparison.Ordinal);

        Assert.IsTrue(recovery >= 0);
        Assert.IsTrue(lastDocument > recovery);
    }

    [TestMethod]
    public void AutoSaveCommitsOnlyAfterRevisionRecheck()
    {
        string persistence = ReadUi("MainWindow.Persistence.cs");
        int prepare = persistence.IndexOf(
            "_autoSaveFileService.Prepare",
            StringComparison.Ordinal);
        int recheck = persistence.IndexOf(
            "IsCurrentDocumentSnapshot(",
            prepare,
            StringComparison.Ordinal);
        int commit = persistence.IndexOf(
            "preparedWrite.Commit()",
            recheck,
            StringComparison.Ordinal);

        Assert.IsTrue(prepare >= 0);
        Assert.IsTrue(recheck > prepare);
        Assert.IsTrue(commit > recheck);
        StringAssert.Contains(
            persistence,
            "_persistenceValidationService.Validate(source)");
        StringAssert.Contains(
            persistence,
            "ClearCurrentRecoverySnapshot(renew: true)");
    }

    [TestMethod]
    public void AutoSaveDoesNotRefreshOrResetEditorInspectorOrPreview()
    {
        string persistence = ReadUi("MainWindow.Persistence.cs");
        int start = persistence.IndexOf(
            "private void QueueAutoSave()",
            StringComparison.Ordinal);
        int end = persistence.IndexOf(
            "private bool IsCurrentDocumentSnapshot(",
            start,
            StringComparison.Ordinal);
        string autoSave = persistence[start..end];

        Assert.IsFalse(autoSave.Contains(
            "RefreshPreview",
            StringComparison.Ordinal));
        Assert.IsFalse(autoSave.Contains(
            "LoadIntoEditor",
            StringComparison.Ordinal));
        Assert.IsFalse(autoSave.Contains(
            "Inspector",
            StringComparison.Ordinal));
        Assert.IsFalse(autoSave.Contains(
            "Caret",
            StringComparison.Ordinal));
        Assert.IsFalse(autoSave.Contains(
            "Zoom",
            StringComparison.Ordinal));
        Assert.IsFalse(autoSave.Contains(
            "Undo",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void DocumentSwitchShutdownAndExactSaveCancelPendingPersistence()
    {
        string persistence = ReadUi("MainWindow.Persistence.cs");
        string main = ReadUi("MainWindow.xaml.cs");

        StringAssert.Contains(
            persistence,
            "private void BeginDocumentSession(");
        StringAssert.Contains(
            persistence,
            "_recoveryDebouncer.Cancel();");
        StringAssert.Contains(
            persistence,
            "_autoSaveDebouncer.Cancel();");
        StringAssert.Contains(
            persistence,
            "private void OnManualDocumentSaved()");
        StringAssert.Contains(
            persistence,
            "private void DiscardCurrentRecoverySnapshot()");
        StringAssert.Contains(
            main,
            "CancelDocumentPersistence();");
        StringAssert.Contains(
            main,
            "DisposeDocumentPersistence();");
    }

    [TestMethod]
    public void SafelyReopenedLastDocumentRemainsAutoSaveEligible()
    {
        string main = ReadUi("MainWindow.xaml.cs");
        int lastRestore = main.IndexOf(
            "_lastDocumentService.TryRestore",
            StringComparison.Ordinal);
        int welcome = main.IndexOf(
            "LoadIntoEditor(welcomeSource",
            lastRestore,
            StringComparison.Ordinal);
        string startupRestore = main[lastRestore..welcome];

        StringAssert.Contains(
            startupRestore,
            "autoSaveEligible: true");
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
