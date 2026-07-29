using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class RecoverySnapshotStoreTests
{
    private string _directory = null!;
    private RecoverySnapshotStore _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Recovery.Tests-{Guid.NewGuid():N}");
        _store = new RecoverySnapshotStore(
            _directory,
            new Utf8FileService(),
            new SafeDocumentPathService());
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void DefaultDirectory_IsExactlyUnderLocalApplicationData()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SvgLiveEditor",
            "Recovery");

        Assert.AreEqual(
            Path.GetFullPath(expected),
            Path.GetFullPath(new RecoveryDirectoryProvider().GetPath()));
    }

    [TestMethod]
    public void UncleanTerminationSimulation_RestoresExactInvalidPersianSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام فارسی</text>";
        RecoverySnapshot snapshot = CreateSnapshot(
            source,
            revision: 7);

        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);

        RecoverySnapshotStore restarted = new(
            _directory,
            new Utf8FileService(),
            new SafeDocumentPathService());
        RecoveryCandidate candidate = restarted
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single();

        Assert.AreEqual(source, candidate.Snapshot.Source);
        Assert.AreEqual(7, candidate.Snapshot.Revision);
        Assert.IsNull(candidate.RestorablePath);
        Assert.IsFalse(new SvgValidationService()
            .Validate(candidate.Snapshot.Source).IsValid);
    }

    [TestMethod]
    public void RecoverySnapshotRoundTripsPersianDigitsAndPunctuationExactly()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text direction=\"rtl\" unicode-bidi=\"embed\" text-anchor=\"start\">قیمت: ۱۲۳٬۴۵۶ تومان.</text></svg>";
        RecoverySnapshot snapshot = CreateSnapshot(source, revision: 8);

        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        RecoverySnapshot restored = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single()
            .Snapshot;

        Assert.AreEqual(source, restored.Source);
        Assert.AreEqual(snapshot.SourceSha256, restored.SourceSha256);
    }

    [TestMethod]
    public void ByteIdenticalNamedSnapshot_IsRemovedWithoutPrompt()
    {
        string originalPath = Path.Combine(_directory, "same.svg");
        Directory.CreateDirectory(_directory);
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>دقیق</text></svg>";
        new Utf8FileService().WriteAllText(originalPath, source);
        RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            originalPath,
            "same.svg",
            source,
            3,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        Assert.IsEmpty(
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow));
        Assert.IsEmpty(
            Directory.GetFiles(_directory, "recovery-*.json"));
    }

    [TestMethod]
    public void MissingOriginal_RestoresAsUntitledWithoutRecreatingIt()
    {
        string missingPath = Path.Combine(_directory, "missing.svg");
        RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            missingPath,
            "missing.svg",
            SafeSvg("recovered"),
            1,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        RecoveryCandidate candidate = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single();

        Assert.IsNull(candidate.RestorablePath);
        Assert.IsFalse(File.Exists(missingPath));
    }

    [TestMethod]
    public void MalformedAndHashTamperedSnapshots_AreRejectedAndDeleted()
    {
        _store.Prune(DateTimeOffset.UtcNow);
        string malformedId = RecoverySnapshotStore.CreateSnapshotId();
        string malformedPath = Path.Combine(
            _directory,
            $"recovery-{malformedId}.json");
        File.WriteAllText(malformedPath, """{"SchemaVersion":1""");

        RecoverySnapshot valid = CreateSnapshot(SafeSvg("safe"), 2);
        RecoverySnapshot tampered = valid with
        {
            SnapshotId = RecoverySnapshotStore.CreateSnapshotId(),
            Source = SafeSvg("tampered")
        };
        string tamperedPath = Path.Combine(
            _directory,
            $"recovery-{tampered.SnapshotId}.json");
        File.WriteAllText(
            tamperedPath,
            JsonSerializer.Serialize(tampered));

        Assert.IsEmpty(
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow));
        Assert.IsFalse(File.Exists(malformedPath));
        Assert.IsFalse(File.Exists(tamperedPath));
    }

    [TestMethod]
    public void UnknownAndNullSchemaFields_AreRejected()
    {
        _store.Prune(DateTimeOffset.UtcNow);
        string unknownId = RecoverySnapshotStore.CreateSnapshotId();
        RecoverySnapshot valid = CreateSnapshot(SafeSvg("schema"), 2) with
        {
            SnapshotId = unknownId
        };
        string unknownJson = JsonSerializer.Serialize(valid)
            .TrimEnd('}')
            + ",\"Unexpected\":\"value\"}";
        string unknownPath = Path.Combine(
            _directory,
            $"recovery-{unknownId}.json");
        File.WriteAllText(unknownPath, unknownJson);

        string nullId = RecoverySnapshotStore.CreateSnapshotId();
        string nullPath = Path.Combine(
            _directory,
            $"recovery-{nullId}.json");
        File.WriteAllText(
            nullPath,
            $$"""
            {"SchemaVersion":1,"SnapshotId":"{{nullId}}","OriginalPath":null,"DisplayName":"Untitled.svg","Source":null,"SourceSha256":null,"Revision":1,"SavedUtc":"{{DateTimeOffset.UtcNow:O}}","IsNamed":false}
            """);

        Assert.IsEmpty(
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow));
        Assert.IsFalse(File.Exists(unknownPath));
        Assert.IsFalse(File.Exists(nullPath));
    }

    [TestMethod]
    public void TamperedHighRevisionCannotBlockLegitimateSnapshot()
    {
        _store.Prune(DateTimeOffset.UtcNow);
        string id = RecoverySnapshotStore.CreateSnapshotId();
        RecoverySnapshot tampered = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("tampered"),
            long.MaxValue,
            DateTimeOffset.UtcNow) with
        {
            SourceSha256 = new string('0', 64)
        };
        string path = Path.Combine(
            _directory,
            $"recovery-{id}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(tampered));
        RecoverySnapshot legitimate = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("legitimate"),
            1,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(_store.TryWrite(legitimate).Succeeded);
        RecoverySnapshot restored = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single()
            .Snapshot;
        Assert.AreEqual(1, restored.Revision);
        StringAssert.Contains(restored.Source, "legitimate");
    }

    [TestMethod]
    public void NewerRevisionWinsEvenIfOlderWriteArrivesLater()
    {
        string id = RecoverySnapshotStore.CreateSnapshotId();
        RecoverySnapshot newer = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("newer"),
            9,
            DateTimeOffset.UtcNow);
        RecoverySnapshot older = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("older"),
            8,
            DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.IsTrue(_store.TryWrite(newer).Succeeded);
        Assert.IsTrue(_store.TryWrite(older).Succeeded);

        RecoverySnapshot restored = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single()
            .Snapshot;
        Assert.AreEqual(9, restored.Revision);
        StringAssert.Contains(restored.Source, "newer");
        Assert.IsFalse(Directory.GetFiles(
            _directory,
            ".recovery-*.tmp").Any());
    }

    [TestMethod]
    public void NewerRevisionAtomicallyReplacesSameManagedSnapshot()
    {
        string id = RecoverySnapshotStore.CreateSnapshotId();
        RecoverySnapshot first = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("first"),
            1,
            DateTimeOffset.UtcNow);
        RecoverySnapshot second = RecoverySnapshotStore.CreateSnapshot(
            id,
            originalPath: null,
            "Untitled.svg",
            SafeSvg("second"),
            2,
            DateTimeOffset.UtcNow.AddMilliseconds(1));

        Assert.IsTrue(_store.TryWrite(first).Succeeded);
        Assert.IsTrue(_store.TryWrite(second).Succeeded);

        RecoverySnapshot restored = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single()
            .Snapshot;
        Assert.AreEqual(2, restored.Revision);
        StringAssert.Contains(restored.Source, "second");
        Assert.HasCount(
            1,
            Directory.GetFiles(_directory, "recovery-*.json"));
        Assert.IsFalse(Directory.GetFiles(
            _directory,
            ".recovery-*.tmp").Any());
    }

    [TestMethod]
    public void RetiredSnapshotCannotBeRecreatedByDelayedWrite()
    {
        RecoverySnapshot snapshot = CreateSnapshot(SafeSvg("pending"), 4);
        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        Assert.IsTrue(_store.TryDelete(snapshot.SnapshotId, retire: true));

        PersistenceOperationResult delayed = _store.TryWrite(snapshot);

        Assert.IsFalse(delayed.Succeeded);
        Assert.IsEmpty(
            Directory.GetFiles(_directory, "recovery-*.json"));
    }

    [TestMethod]
    public void RetentionRemovesExpiredAndLimitsSnapshotCount()
    {
        RecoverySnapshot expired = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            originalPath: null,
            "expired.svg",
            SafeSvg("expired"),
            1,
            DateTimeOffset.UtcNow
                - RecoverySnapshotStore.MaximumSnapshotAge
                - TimeSpan.FromMinutes(1));
        Assert.IsFalse(_store.TryWrite(expired).Succeeded);

        for (int index = 0;
             index < RecoverySnapshotStore.MaximumSnapshotCount + 3;
             index++)
        {
            RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
                RecoverySnapshotStore.CreateSnapshotId(),
                originalPath: null,
                $"item-{index}.svg",
                SafeSvg(index.ToString()),
                index,
                DateTimeOffset.UtcNow.AddMilliseconds(index));
            Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        }

        IReadOnlyList<RecoveryCandidate> candidates =
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow);
        Assert.HasCount(
            RecoverySnapshotStore.MaximumSnapshotCount,
            candidates);
        Assert.IsFalse(candidates.Any(
            candidate => candidate.Snapshot.DisplayName == "expired.svg"));
        Assert.IsTrue(
            Directory.GetFiles(_directory, "recovery-*.json").Length
            <= RecoverySnapshotStore.MaximumSnapshotCount);
    }

    [TestMethod]
    public void IdentifierValidationPreventsPathTraversalDeletion()
    {
        string outside = Path.Combine(
            Path.GetDirectoryName(_directory)!,
            $"outside-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outside, "keep");
        try
        {
            Assert.IsFalse(_store.TryDelete(@"..\outside.txt"));
            Assert.AreEqual("keep", File.ReadAllText(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [TestMethod]
    public void UnsupportedSchemaAndOversizedSourceAreRejected()
    {
        RecoverySnapshot unsupported = CreateSnapshot(
            SafeSvg("schema"),
            revision: 1) with
        {
            SchemaVersion = RecoverySnapshot.CurrentSchemaVersion + 1
        };
        Assert.IsFalse(_store.TryWrite(unsupported).Succeeded);

        string oversizedSource = new(
            'x',
            checked((int)Utf8FileService.MaximumFileBytes + 1));
        RecoverySnapshot oversized =
            RecoverySnapshotStore.CreateSnapshot(
                RecoverySnapshotStore.CreateSnapshotId(),
                originalPath: null,
                "large.svg",
                oversizedSource,
                1,
                DateTimeOffset.UtcNow);
        Assert.IsFalse(_store.TryWrite(oversized).Succeeded);
        Assert.IsFalse(Directory.Exists(_directory)
            && Directory.GetFiles(_directory).Any());
    }

    [TestMethod]
    public void RestoreCandidateDoesNotOverwriteDifferentOriginalAndIsModifiedInMemory()
    {
        string path = Path.Combine(_directory, "original.svg");
        Directory.CreateDirectory(_directory);
        const string diskSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>disk</text></svg>";
        const string recoveredSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>recovered</text></svg>";
        new Utf8FileService().WriteAllText(path, diskSource);
        RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            path,
            "original.svg",
            recoveredSource,
            3,
            DateTimeOffset.UtcNow);
        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);

        RecoveryCandidate candidate = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single();
        MainViewModel viewModel = new();
        viewModel.LoadDocument(
            candidate.Snapshot.Source,
            candidate.RestorablePath,
            isModified: true);

        Assert.AreEqual(diskSource, new Utf8FileService().ReadAllText(path));
        Assert.AreEqual(recoveredSource, viewModel.DocumentText);
        Assert.IsTrue(viewModel.IsModified);
        Assert.AreEqual(Path.GetFullPath(path), viewModel.CurrentFilePath);
    }

    [TestMethod]
    public void UnsafeOriginalMetadataIsNeverFollowed()
    {
        RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            @"\\example.invalid\share\remote.svg",
            "remote.svg",
            SafeSvg("local snapshot"),
            1,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(_store.TryWrite(snapshot).Succeeded);
        RecoveryCandidate candidate = _store
            .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
            .Single();
        Assert.IsNull(candidate.RestorablePath);
        StringAssert.Contains(
            candidate.Snapshot.Source,
            "local snapshot");
    }

    [TestMethod]
    public void DiscardDeletesOnlySelectedWhileSkipPreservesSnapshots()
    {
        RecoverySnapshot first = CreateSnapshot(
            SafeSvg("first"),
            revision: 1);
        RecoverySnapshot second = CreateSnapshot(
            SafeSvg("second"),
            revision: 1);
        Assert.IsTrue(_store.TryWrite(first).Succeeded);
        Assert.IsTrue(_store.TryWrite(second).Succeeded);

        Assert.HasCount(
            2,
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow));
        Assert.IsTrue(_store.TryDelete(
            first.SnapshotId,
            retire: true));
        IReadOnlyList<RecoveryCandidate> afterDiscard =
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow);

        Assert.HasCount(1, afterDiscard);
        Assert.AreEqual(
            second.SnapshotId,
            afterDiscard[0].Snapshot.SnapshotId);
        Assert.HasCount(
            1,
            _store.LoadMeaningfulCandidates(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void CleanupFailureIsContained()
    {
        string fileInsteadOfDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Recovery.File-{Guid.NewGuid():N}");
        File.WriteAllText(fileInsteadOfDirectory, "not a directory");
        try
        {
            RecoverySnapshotStore blockedStore = new(
                fileInsteadOfDirectory,
                new Utf8FileService(),
                new SafeDocumentPathService());

            blockedStore.Prune(DateTimeOffset.UtcNow);
            Assert.IsEmpty(
                blockedStore.LoadMeaningfulCandidates(
                    DateTimeOffset.UtcNow));
        }
        finally
        {
            File.Delete(fileInsteadOfDirectory);
        }
    }

    [TestMethod]
    public void RetentionBudgetEnforcesCountAndTotalBytesWithoutAllocatingPayloads()
    {
        Assert.IsTrue(RecoveryRetentionPolicy.CanKeep(
            currentCount: 9,
            currentBytes: 99_000_000,
            nextFileBytes: 1_000_000));
        Assert.IsFalse(RecoveryRetentionPolicy.CanKeep(
            currentCount: 10,
            currentBytes: 1,
            nextFileBytes: 1));
        Assert.IsFalse(RecoveryRetentionPolicy.CanKeep(
            currentCount: 9,
            currentBytes: 99_000_000,
            nextFileBytes: 1_000_001));
    }

    private static RecoverySnapshot CreateSnapshot(
        string source,
        long revision)
    {
        return RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            originalPath: null,
            "Untitled.svg",
            source,
            revision,
            DateTimeOffset.UtcNow);
    }

    private static string SafeSvg(string text) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\"><text>{text}</text></svg>";
}
