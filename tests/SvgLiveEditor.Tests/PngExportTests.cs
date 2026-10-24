using System.Text.Json;
using System.Windows.Controls;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PngExportTests
{
    [TestMethod]
    [DataRow("1", "1", true)]
    [DataRow("4096", "1953", true)]
    [DataRow("2828", "2828", true)]
    [DataRow("0", "100", false)]
    [DataRow("4097", "1", false)]
    [DataRow("4096", "4096", false)]
    [DataRow("1.5", "20", false)]
    [DataRow("-1", "20", false)]
    [DataRow("999999999999", "1", false)]
    public void ManualDimensionsAreValidatedWithoutReduction(string width, string height, bool valid)
    {
        Assert.AreEqual(valid, PreviewPngSizeCalculator.TryReadExportSize(width, height, out PreviewPngSize size, out string error));
        Assert.AreEqual(valid, error.Length == 0);
        if (valid) Assert.AreEqual(int.Parse(width), size.Width);
    }

    [TestMethod]
    public void LinkedDimensionsRoundFromTheOriginalRatioAndFitNeverStretches()
    {
        Assert.AreEqual(2d, PreviewPngSizeCalculator.LinkedDimension(3, new(2, 1), true));
        Assert.AreEqual(6d, PreviewPngSizeCalculator.LinkedDimension(3, new(2, 1), false));
        Assert.AreEqual(8192d, PreviewPngSizeCalculator.LinkedDimension(4096, new(1, 2), true));
        Assert.AreEqual(new SvgCanvasSize(100, 50), PreviewPngSizeCalculator.FitExport(new(100, 50), new(100, 100)));
        Assert.AreEqual(new SvgCanvasSize(50, 100), PreviewPngSizeCalculator.FitExport(new(50, 100), new(100, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewPngSizeCalculator.FitExport(new(100, 50), new(4096, 4096)));
    }

    [TestMethod]
    public void ExportMessageHasItsOwnBoundedRevisionAndFitSchema()
    {
        PreviewPageMessageBuilder builder = new();
        const string token = "00112233445566778899AABBCCDDEEFF";
        using JsonDocument json = JsonDocument.Parse(builder.BuildPngExportMessage(token, token, 7, new(100, 100), new(100, 50)));
        JsonElement root = json.RootElement;
        Assert.AreEqual(8, root.EnumerateObject().Count());
        Assert.AreEqual("exportPng", root.GetProperty("type").GetString());
        Assert.AreEqual(7, root.GetProperty("sourceRevision").GetInt64());
        Assert.AreEqual(100d, root.GetProperty("drawWidth").GetDouble());
        Assert.AreEqual(50d, root.GetProperty("drawHeight").GetDouble());
        Assert.Throws<ArgumentException>(() => builder.BuildPngExportMessage("bad", token, 7, new(1, 1), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildPngExportMessage(token, token, -1, new(1, 1), new(1, 1)));
    }

    [TestMethod]
    public void NativeDimensionsDialogLocksByDefaultAndShowsInvalidInputInline()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                ExportPngWindow window = new(new(100, 50));
                TextBox width = (TextBox)window.FindName("WidthBox");
                TextBox height = (TextBox)window.FindName("HeightBox");
                CheckBox locked = (CheckBox)window.FindName("LockRatio");
                Button export = (Button)window.FindName("ExportButton");
                TextBlock error = (TextBlock)window.FindName("ErrorText");
                Assert.IsTrue(locked.IsChecked == true);
                Assert.AreEqual("100", width.Text);
                width.Text = "123";
                Assert.AreEqual("62", height.Text);
                height.Text = "40";
                Assert.AreEqual("80", width.Text);
                locked.IsChecked = false;
                height.Text = "100";
                Assert.AreEqual("80", width.Text);
                width.Text = "4097";
                Assert.IsFalse(export.IsEnabled);
                Assert.IsTrue(error.Text.Length > 0);
                width.Text = "4096";
                height.Text = "4096";
                Assert.IsFalse(export.IsEnabled);
                StringAssert.Contains(error.Text, "8,000,000");
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    private static void InTemporaryDirectory(Action<string, PngExportFileService, PreviewPngPayload> run)
    {
        string directory = Path.Combine(Path.GetTempPath(), "SvgLiveEditor.Export.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { run(Path.Combine(directory, "export.png"), new(), new(new(1, 1), PngTestData.CreateDecodedOnePixelPng())); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ValidatedBytesCreateAndAtomicallyReplaceWithoutTemporaryResidue() => InTemporaryDirectory((path, service, payload) =>
    {
        using (PreparedPngExport prepared = service.Prepare(service.CaptureDestination(path, null), payload, null)) prepared.Commit(() => true);
        CollectionAssert.AreEqual(payload.Bytes, File.ReadAllBytes(path));
        File.WriteAllText(path, "old destination");
        using (PreparedPngExport prepared = service.Prepare(service.CaptureDestination(path, null), payload, null)) prepared.Commit(() => true);
        CollectionAssert.AreEqual(payload.Bytes, File.ReadAllBytes(path));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path)!).Length);
    });

    [TestMethod]
    public void CancellationStaleApplicabilityAndInvalidPayloadLeaveDestinationUntouched() => InTemporaryDirectory((path, service, payload) =>
    {
        File.WriteAllText(path, "keep");
        PngExportDestination destination = service.CaptureDestination(path, null);
        using (PreparedPngExport prepared = service.Prepare(destination, payload, null))
            Assert.Throws<OperationCanceledException>(() => prepared.Commit(() => false));
        using (CancellationTokenSource stagedCancellation = new())
        using (PreparedPngExport prepared = service.Prepare(destination, payload, null, stagedCancellation.Token))
        {
            stagedCancellation.Cancel();
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path)!).Length,
                "Cancellation must clean a staged file even before the UI continuation runs.");
        }
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.Prepare(destination, payload, null, canceled.Token));
        Assert.Throws<InvalidDataException>(() => service.Prepare(destination, new(new(1, 1), [1, 2]), null));
        Assert.Throws<InvalidDataException>(() =>
        {
            using PreparedPngExport undecodable = service.Prepare(destination,
                new(new(1, 1), PngTestData.CreateStructurallyValidPng(1, 1)), null);
        });
        Assert.AreEqual("keep", File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path)!).Length);
    });

    [TestMethod]
    public void DestinationChangesAndFailedReplacementDoNotOverwriteExistingFiles() => InTemporaryDirectory((path, service, payload) =>
    {
        PngExportDestination absent = service.CaptureDestination(path, null);
        using (PreparedPngExport prepared = service.Prepare(absent, payload, null))
        {
            File.WriteAllText(path, "created while exporting");
            Assert.Throws<IOException>(() => prepared.Commit(() => true));
        }
        PngExportDestination existing = service.CaptureDestination(path, null);
        using (PreparedPngExport prepared = service.Prepare(existing, payload, null))
        {
            File.WriteAllText(path, "changed");
            Assert.Throws<IOException>(() => prepared.Commit(() => true));
        }
        using (PreparedPngExport prepared = service.Prepare(service.CaptureDestination(path, null), payload, null))
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => prepared.Commit(() => true));
        Assert.AreEqual("changed", File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path)!).Length);
        Assert.Throws<ArgumentException>(() => service.CaptureDestination(path, path));
        Assert.Throws<ArgumentException>(() => service.CaptureDestination(Path.ChangeExtension(path, ".svg"), null));
    });
}
