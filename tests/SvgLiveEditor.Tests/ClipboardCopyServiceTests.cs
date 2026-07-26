using System.Runtime.InteropServices;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ClipboardCopyServiceTests
{
    [TestMethod]
    public async Task CopyText_PreservesExactMultilineUnicodeAndInvalidXml()
    {
        const string source =
            "<svg>\r\n  <text>سلام، دنیا</text>\n  <broken>\r\n";
        FakeClipboardWriter writer = new();
        ClipboardCopyService service = CreateService(writer);

        ClipboardWriteResult result =
            await service.CopyTextAsync(source);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(source, writer.Text);
        Assert.AreEqual(1, writer.TextWriteCount);
        Assert.AreEqual(0, writer.PngWriteCount);
    }

    [TestMethod]
    public async Task CopyText_HandlesEmptyDocumentsWithoutRealClipboardAccess()
    {
        FakeClipboardWriter writer = new();
        ClipboardCopyService service = CreateService(writer);

        ClipboardWriteResult result =
            await service.CopyTextAsync(string.Empty);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(string.Empty, writer.Text);
        Assert.AreEqual(1, writer.TextWriteCount);
    }

    [TestMethod]
    public async Task CopyPng_UsesOnlyTheInjectedClipboardWriter()
    {
        FakeClipboardWriter writer = new();
        ClipboardCopyService service = CreateService(writer);
        PreviewPngPayload payload = new(
            new PreviewPngSize(1, 1),
            [137, 80, 78, 71]);

        ClipboardWriteResult result =
            await service.CopyPngAsync(payload);

        Assert.IsTrue(result.Succeeded);
        Assert.AreSame(payload, writer.Png);
        Assert.AreEqual(1, writer.PngWriteCount);
        Assert.AreEqual(0, writer.TextWriteCount);
    }

    [TestMethod]
    public async Task CopyText_DoesNotChangeDocumentOrSelectionState()
    {
        MainViewModel viewModel = new();
        viewModel.LoadDocument("<svg />", path: null);
        viewModel.UpdateTextFromEditor("<svg>\n</svg>");
        int caretOffset = 4;
        int selectionStart = 1;
        int selectionLength = 3;
        FakeClipboardWriter writer = new();
        ClipboardCopyService service = CreateService(writer);

        await service.CopyTextAsync(viewModel.DocumentText);

        Assert.IsTrue(viewModel.IsModified);
        Assert.AreEqual("<svg>\n</svg>", viewModel.DocumentText);
        Assert.AreEqual(4, caretOffset);
        Assert.AreEqual(1, selectionStart);
        Assert.AreEqual(3, selectionLength);
    }

    [TestMethod]
    public async Task BusyClipboard_RetriesOnlyToTheConfiguredBound()
    {
        FakeClipboardWriter writer = new()
        {
            BusyFailuresRemaining = 10
        };
        int delayCount = 0;
        ClipboardRetryService retry = new(
            maximumAttempts: 3,
            retryDelay: TimeSpan.Zero,
            delayAsync: (_, _) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });
        ClipboardCopyService service = new(writer, retry);

        ClipboardWriteResult result =
            await service.CopyTextAsync("source");

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(3, result.Attempts);
        Assert.AreEqual(3, writer.TextWriteCount);
        Assert.AreEqual(2, delayCount);
    }

    [TestMethod]
    public async Task BusyClipboard_SucceedsWithinTheConfiguredBound()
    {
        FakeClipboardWriter writer = new()
        {
            BusyFailuresRemaining = 2
        };
        ClipboardCopyService service = CreateService(
            writer,
            maximumAttempts: 4);

        ClipboardWriteResult result =
            await service.CopyTextAsync("source");

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(3, result.Attempts);
        Assert.AreEqual("source", writer.Text);
    }

    private static ClipboardCopyService CreateService(
        FakeClipboardWriter writer,
        int maximumAttempts = 1)
    {
        ClipboardRetryService retry = new(
            maximumAttempts,
            TimeSpan.Zero,
            (_, _) => Task.CompletedTask);
        return new ClipboardCopyService(writer, retry);
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public int BusyFailuresRemaining { get; set; }

        public int TextWriteCount { get; private set; }

        public int PngWriteCount { get; private set; }

        public string? Text { get; private set; }

        public PreviewPngPayload? Png { get; private set; }

        public void WriteText(string text)
        {
            TextWriteCount++;
            if (BusyFailuresRemaining-- > 0)
            {
                throw new COMException(
                    "Clipboard busy",
                    unchecked((int)0x800401D0));
            }

            Text = text;
        }

        public void WritePng(PreviewPngPayload payload)
        {
            PngWriteCount++;
            Png = payload;
        }
    }
}
