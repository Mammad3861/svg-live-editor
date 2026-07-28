using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewDragFileStoreTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.DragStoreTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void DeleteTemporaryDirectory()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        else if (File.Exists(_temporaryDirectory))
        {
            File.Delete(_temporaryDirectory);
        }
    }

    [TestMethod]
    public void ValidatedPng_UsesRandomSafeScopedNameWithoutOverwrite()
    {
        PreviewDragFileStore store = new(_temporaryDirectory);
        PreviewPngPayload payload = CreatePayload();

        PreviewDragFileResult first = store.TryCreate(payload);
        PreviewDragFileResult second = store.TryCreate(payload);

        Assert.IsTrue(first.Succeeded);
        Assert.IsTrue(second.Succeeded);
        Assert.AreNotEqual(first.Path, second.Path);
        Assert.AreEqual(
            Path.GetFullPath(_temporaryDirectory),
            Path.GetDirectoryName(first.Path));
        StringAssert.Matches(
            Path.GetFileName(first.Path),
            new System.Text.RegularExpressions.Regex(
                "^SvgLiveEditor-[0-9a-f]{32}\\.png$"));
        CollectionAssert.AreEqual(
            payload.Bytes,
            File.ReadAllBytes(first.Path!));
    }

    [TestMethod]
    public void MalformedOrOversizedPng_IsRejectedBeforeDiskWrite()
    {
        PreviewDragFileStore store = new(_temporaryDirectory);
        byte[] malformed = PngTestData.CreateStructurallyValidPng(
            1,
            1);
        malformed[0] = 0;

        PreviewDragFileResult result = store.TryCreate(
            new PreviewPngPayload(
                new PreviewPngSize(1, 1),
                malformed));

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(
            0,
            Directory.EnumerateFiles(_temporaryDirectory).Count());
    }

    [TestMethod]
    public void TryDelete_RejectsTraversalAndDeletesOnlyManagedFile()
    {
        string outside = Path.Combine(
            Path.GetDirectoryName(_temporaryDirectory)!,
            $"outside-{Guid.NewGuid():N}.png");
        File.WriteAllText(outside, "keep");
        PreviewDragFileStore store = new(_temporaryDirectory);
        PreviewDragFileResult managed =
            store.TryCreate(CreatePayload());
        Assert.IsTrue(managed.Succeeded);

        try
        {
            Assert.IsFalse(store.TryDelete(outside));
            Assert.IsFalse(store.TryDelete(
                Path.Combine(
                    _temporaryDirectory,
                    "..",
                    Path.GetFileName(outside))));
            Assert.IsTrue(File.Exists(outside));
            Assert.IsTrue(store.TryDelete(managed.Path!));
            Assert.IsFalse(File.Exists(managed.Path));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [TestMethod]
    public void Cleanup_RemovesStaleFilesAndEnforcesCountAndSize()
    {
        DateTimeOffset now =
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        PreviewDragFileStore store = new(
            _temporaryDirectory,
            utcNow: () => now,
            maximumAge: TimeSpan.FromHours(2),
            maximumFileCount: 3,
            maximumTotalBytes: 10);
        Directory.CreateDirectory(_temporaryDirectory);

        string stale = CreateManagedFile(1, now.AddHours(-3));
        for (int index = 0; index < 5; index++)
        {
            CreateManagedFile(
                length: 4,
                now.AddMinutes(-index));
        }

        Assert.IsTrue(store.TryCleanup());

        FileInfo[] remaining = Directory
            .EnumerateFiles(_temporaryDirectory)
            .Select(path => new FileInfo(path))
            .ToArray();
        Assert.IsFalse(File.Exists(stale));
        Assert.IsTrue(remaining.Length <= 3);
        Assert.IsTrue(remaining.Sum(file => file.Length) <= 10);
    }

    [TestMethod]
    public void CleanupAndCreateFailures_DoNotThrowAtStartup()
    {
        Directory.Delete(_temporaryDirectory);
        File.WriteAllText(_temporaryDirectory, "not a directory");
        PreviewDragFileStore store = new(_temporaryDirectory);

        Assert.IsTrue(store.TryCleanup());
        PreviewDragFileResult result =
            store.TryCreate(CreatePayload());

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [TestMethod]
    public void CancelledDrag_CanRemoveItsTemporaryFileImmediately()
    {
        PreviewDragFileStore store = new(_temporaryDirectory);
        PreviewDragFileResult created =
            store.TryCreate(CreatePayload());

        Assert.IsTrue(created.Succeeded);
        Assert.IsTrue(File.Exists(created.Path));
        Assert.IsTrue(store.TryDelete(created.Path!));
        Assert.IsFalse(File.Exists(created.Path));
    }

    private string CreateManagedFile(
        long length,
        DateTimeOffset timestamp)
    {
        string path = Path.Combine(
            _temporaryDirectory,
            $"SvgLiveEditor-{Guid.NewGuid():N}.png");
        using (FileStream stream = File.Create(path))
        {
            stream.SetLength(length);
        }
        File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        return path;
    }

    private static PreviewPngPayload CreatePayload() =>
        new(
            new PreviewPngSize(1, 1),
            PngTestData.CreateStructurallyValidPng(1, 1));
}
