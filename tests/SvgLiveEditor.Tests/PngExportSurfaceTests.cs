namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PngExportSurfaceTests
{
    [TestMethod]
    public void ExportHasASeparateFileCommandAndDoesNotReplaceSaveAs()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ui", "MainWindow.xaml"));
        StringAssert.Contains(xaml, "Click=\"OnExportPngClick\"");
        StringAssert.Contains(xaml, "Header=\"Export _PNG...\"");
        StringAssert.Contains(xaml, "Click=\"OnSaveAsClick\"");
    }
}
