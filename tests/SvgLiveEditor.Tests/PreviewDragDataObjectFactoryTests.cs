using System.Collections.Specialized;
using System.Windows;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewDragDataObjectFactoryTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.DataObjectTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void DeleteTemporaryDirectory()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [STATestMethod]
    public void DataObject_ProvidesFileDropPngAndBitmapFormats()
    {
        PreviewPngPayload payload = new(
            new PreviewPngSize(1, 1),
            PngTestData.CreateDecodedOnePixelPng());
        PreviewDragFileStore store =
            new(_temporaryDirectory);
        PreviewDragFileResult file = store.TryCreate(payload);
        Assert.IsTrue(file.Succeeded);

        DataObject data =
            new PreviewDragDataObjectFactory().Create(
                payload,
                file.Path!);

        Assert.IsTrue(data.GetDataPresent(
            DataFormats.FileDrop,
            autoConvert: false));
        Assert.IsTrue(data.GetDataPresent(
            PreviewDragDataObjectFactory.PngDataFormat,
            autoConvert: false));
        Assert.IsTrue(data.GetDataPresent(
            DataFormats.Bitmap,
            autoConvert: false));
        StringCollection droppedFiles =
            data.GetFileDropList();
        Assert.HasCount(1, droppedFiles);
        Assert.AreEqual(
            Path.GetFullPath(file.Path!),
            droppedFiles[0]);
    }

    [STATestMethod]
    public void DataObject_RejectsMalformedPayloadOrMissingFile()
    {
        PreviewDragDataObjectFactory factory = new();
        PreviewPngPayload malformed = new(
            new PreviewPngSize(1, 1),
            [1, 2, 3]);
        string missing = Path.Combine(
            _temporaryDirectory,
            "missing.png");

        Assert.ThrowsExactly<ArgumentException>(() =>
            factory.Create(malformed, missing));
        Assert.ThrowsExactly<ArgumentException>(() =>
            factory.Create(
                new PreviewPngPayload(
                    new PreviewPngSize(1, 1),
                    PngTestData.CreateDecodedOnePixelPng()),
                missing));
    }

    [STATestMethod]
    public void DataObject_RejectsStructurallyValidButUndecodablePng()
    {
        PreviewPngPayload undecodable = new(
            new PreviewPngSize(1, 1),
            PngTestData.CreateStructurallyValidPng(1, 1));
        PreviewDragFileResult file =
            new PreviewDragFileStore(_temporaryDirectory)
                .TryCreate(undecodable);
        Assert.IsTrue(file.Succeeded);

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(() =>
                new PreviewDragDataObjectFactory().Create(
                    undecodable,
                    file.Path!));

        StringAssert.Contains(
            exception.Message,
            "could not be decoded safely");
    }

    [TestMethod]
    public void DragStatus_ExplainsLastValidPreviewBehavior()
    {
        StringAssert.Contains(
            PreviewDragStatusPolicy.Started(
                PreviewPngSourceState.CurrentInvalid,
                new PreviewPngSize(100, 50)),
            "last valid preview");
        StringAssert.Contains(
            PreviewDragStatusPolicy.Started(
                PreviewPngSourceState.PendingValidation,
                new PreviewPngSize(100, 50)),
            "still validating");
        StringAssert.Contains(
            PreviewDragStatusPolicy.Started(
                PreviewPngSourceState.CurrentValid,
                new PreviewPngSize(100, 50)),
            "Dragging preview image");
    }

    [TestMethod]
    public void DragImageControl_DoesNotReplacePreviewPanGesture()
    {
        string xaml = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "ui",
                "MainWindow.xaml"));

        StringAssert.Contains(
            xaml,
            "x:Name=\"DragImageButton\"");
        StringAssert.Contains(
            xaml,
            "PreviewMouseMove=\"OnDragImagePreviewMouseMove\"");
        Assert.IsFalse(xaml.Contains(
            "x:Name=\"PreviewWebView\" PreviewMouseLeftButtonDown",
            StringComparison.Ordinal));
        StringAssert.Contains(xaml, "x:Name=\"PanModeButton\"");
    }
}
