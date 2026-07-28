using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InboundFileDropPolicyTests
{
    private readonly InboundFileDropPolicy _policy = new();
    private readonly Utf8FileService _fileService = new();
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.DropTests-{Guid.NewGuid():N}");
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

    [TestMethod]
    [DataRow("sample.svg")]
    [DataRow("sample.SVG")]
    [DataRow("sample.SvG")]
    [DataRow("sample.txt")]
    [DataRow("sample.TXT")]
    public void OneSupportedLocalFile_IsAccepted(
        string fileName)
    {
        string path = CreateFile(fileName, "<svg />");

        InboundFileDropEvaluation result =
            _policy.Evaluate([path]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(Path.GetFullPath(path), result.FullPath);
        Assert.AreEqual(fileName, result.DisplayFileName);
    }

    [TestMethod]
    public void FileDropDataObject_IsAcceptedWithoutUsingTextFormats()
    {
        string path = CreateFile("from-explorer.svg", "<svg />");
        DataObject data = new();
        data.SetData(DataFormats.FileDrop, new[] { path });
        data.SetData(
            DataFormats.UnicodeText,
            "This unrelated text must not become a path.");

        InboundFileDropEvaluation result = _policy.Evaluate(data);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(Path.GetFullPath(path), result.FullPath);
    }

    [TestMethod]
    public void UnreadableOlePayload_IsRejectedWithoutEscapingTheUiBoundary()
    {
        InboundFileDropEvaluation result =
            _policy.Evaluate(new ThrowingDataObject());

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(
            InboundFileDropRejection.UnreadablePayload,
            result.Rejection);
    }

    [TestMethod]
    public void SvgContainingTxtAndPersianUtf8_ArePreservedExactly()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام، پروندهٔ فارسی</text></svg>";
        string path = CreateFile("persian.txt", source);

        InboundFileDropEvaluation result =
            _policy.Evaluate([path]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(source, _fileService.ReadAllText(path));
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes(source),
            File.ReadAllBytes(path));
    }

    [TestMethod]
    public void InvalidXml_IsOpenedExactlyButCannotReplaceSafePreview()
    {
        const string invalidSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>broken";
        string path = CreateFile("invalid.svg", invalidSource);

        InboundFileDropEvaluation drop = _policy.Evaluate([path]);
        string openedSource = _fileService.ReadAllText(path);
        SvgValidationResult validation =
            new SvgValidationService().Validate(openedSource);
        bool canUseLastValidPreview =
            new PreviewPngCopyPolicy().TryCreatePlan(
                hasVisiblePreview: true,
                PreviewPngSourceState.CurrentInvalid,
                new SvgCanvasSize(320, 180),
                out PreviewPngCopyPlan? plan);

        Assert.IsTrue(drop.IsAccepted);
        Assert.AreEqual(invalidSource, openedSource);
        Assert.IsFalse(validation.IsValid);
        Assert.IsNotNull(validation.LineNumber);
        Assert.IsTrue(canUseLastValidPreview);
        Assert.IsNotNull(plan);
        Assert.IsTrue(plan.UsesLastValidPreview);
    }

    [TestMethod]
    public void SuccessfulDrop_CanBecomeTheRecentDocument()
    {
        string path = CreateFile("recent.svg", "<svg />");
        UserPreferences initial = UserPreferences.Default;

        InboundFileDropEvaluation drop = _policy.Evaluate([path]);
        UserPreferences remembered =
            new LastDocumentService().Remember(
                initial,
                drop.FullPath!);

        Assert.IsTrue(drop.IsAccepted);
        Assert.AreEqual(
            Path.GetFullPath(path),
            remembered.LastDocumentPath);
    }

    [TestMethod]
    [DataRow(UnsavedChangesChoice.Save, true, true)]
    [DataRow(UnsavedChangesChoice.Save, false, false)]
    [DataRow(UnsavedChangesChoice.Discard, false, true)]
    [DataRow(UnsavedChangesChoice.Cancel, false, false)]
    public void UnsavedPromptDecision_IsAppliedBeforeDropReplacement(
        UnsavedChangesChoice choice,
        bool saveSucceeded,
        bool expected)
    {
        string path = CreateFile("replacement.svg", "<svg />");
        InboundFileDropEvaluation drop = _policy.Evaluate([path]);

        bool canReplace = drop.IsAccepted
            && new UnsavedChangesPolicy().CanProceed(
                hasUnsavedChanges: true,
                choice,
                saveSucceeded);

        Assert.AreEqual(expected, canReplace);
    }

    [TestMethod]
    public void MultipleAndEmptyPayloads_AreRejected()
    {
        string first = CreateFile("first.svg", "<svg />");
        string second = CreateFile("second.svg", "<svg />");

        Assert.AreEqual(
            InboundFileDropRejection.EmptyPayload,
            _policy.Evaluate(Array.Empty<string>()).Rejection);
        Assert.AreEqual(
            InboundFileDropRejection.MultipleFiles,
            _policy.Evaluate([first, second]).Rejection);
    }

    [TestMethod]
    public void DirectoryShortcutUnsupportedAndMissingFiles_AreRejected()
    {
        string directory = Path.Combine(
            _temporaryDirectory,
            "folder.svg");
        Directory.CreateDirectory(directory);
        string shortcut = Path.Combine(
            _temporaryDirectory,
            "shortcut.lnk");
        string unsupported = CreateFile("image.png", "not an svg");
        string missing = Path.Combine(
            _temporaryDirectory,
            "missing.svg");

        Assert.AreEqual(
            InboundFileDropRejection.Directory,
            _policy.Evaluate([directory]).Rejection);
        Assert.AreEqual(
            InboundFileDropRejection.Shortcut,
            _policy.Evaluate([shortcut]).Rejection);
        Assert.AreEqual(
            InboundFileDropRejection.UnsupportedExtension,
            _policy.Evaluate([unsupported]).Rejection);
        Assert.AreEqual(
            InboundFileDropRejection.MissingFile,
            _policy.Evaluate([missing]).Rejection);
    }

    [TestMethod]
    public void UrlHtmlAndArbitraryTextPayloads_AreRejected()
    {
        foreach ((string format, string content) in new[]
        {
            (DataFormats.UnicodeText, "https://example.test/file.svg"),
            (DataFormats.Html, "<a href=\"https://example.test\">SVG</a>"),
            (DataFormats.Text, "from-text.svg")
        })
        {
            DataObject data = new();
            data.SetData(format, content);

            InboundFileDropEvaluation result =
                _policy.Evaluate(data);

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(
                InboundFileDropRejection.EmptyPayload,
                result.Rejection);
        }
    }

    [TestMethod]
    public void UncAndFileUrlInputs_AreRejectedAsNonLocalPayloads()
    {
        Assert.AreEqual(
            InboundFileDropRejection.NotLocalFile,
            _policy.Evaluate(
                [@"\\server\share\remote.svg"]).Rejection);
        Assert.AreEqual(
            InboundFileDropRejection.NotLocalFile,
            _policy.Evaluate(
                ["https://example.test/remote.svg"]).Rejection);
    }

    [TestMethod]
    public void MappedNetworkDrive_IsRejectedBeforeFileMetadataAccess()
    {
        bool attributesWereRead = false;
        InboundFileDropPolicy policy = new(
            _ => DriveType.Network,
            _ =>
            {
                attributesWereRead = true;
                return FileAttributes.Normal;
            });

        InboundFileDropEvaluation result =
            policy.Evaluate([@"Z:\remote.svg"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(
            InboundFileDropRejection.NotLocalFile,
            result.Rejection);
        Assert.IsFalse(attributesWereRead);
    }

    [TestMethod]
    public void ReparsePointAncestor_IsRejectedBeforeOpeningFile()
    {
        string path = CreateFile("linked-parent.svg", "<svg />");
        string redirectedDirectory = Path.GetFullPath(
            _temporaryDirectory);
        InboundFileDropPolicy policy = new(
            _ => DriveType.Fixed,
            candidate =>
                string.Equals(
                    Path.GetFullPath(candidate)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar),
                    redirectedDirectory,
                    StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory
                        | FileAttributes.ReparsePoint
                    : File.GetAttributes(candidate));

        InboundFileDropEvaluation result =
            policy.Evaluate([path]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(
            InboundFileDropRejection.ReparsePoint,
            result.Rejection);
    }

    [TestMethod]
    public void OversizedFile_IsRejectedBeforeItIsRead()
    {
        string path = Path.Combine(
            _temporaryDirectory,
            "oversized.svg");
        using (FileStream stream = File.Create(path))
        {
            stream.SetLength(Utf8FileService.MaximumFileBytes + 1);
        }

        InboundFileDropEvaluation result = _policy.Evaluate([path]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(
            InboundFileDropRejection.OversizedFile,
            result.Rejection);
        Assert.ThrowsExactly<FileSizeLimitExceededException>(
            () => _fileService.ReadAllText(path));
    }

    [TestMethod]
    public void MissingOrLockedFile_CannotBeRead()
    {
        string path = CreateFile("locked.svg", "<svg />");
        using FileStream lockStream = new(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.Throws<IOException>(
            () => _fileService.ReadAllText(path));
    }

    private string CreateFile(
        string fileName,
        string source)
    {
        string path = Path.Combine(
            _temporaryDirectory,
            fileName);
        _fileService.WriteAllText(path, source);
        return path;
    }

    private sealed class ThrowingDataObject : IDataObject
    {
        public object GetData(string format, bool autoConvert) =>
            throw new ExternalException("Disconnected drag source.");

        public object GetData(string format) =>
            throw new ExternalException("Disconnected drag source.");

        public object GetData(Type format) =>
            throw new ExternalException("Disconnected drag source.");

        public bool GetDataPresent(
            string format,
            bool autoConvert) =>
            throw new ExternalException("Disconnected drag source.");

        public bool GetDataPresent(string format) =>
            throw new ExternalException("Disconnected drag source.");

        public bool GetDataPresent(Type format) =>
            throw new ExternalException("Disconnected drag source.");

        public string[] GetFormats(bool autoConvert) =>
            throw new ExternalException("Disconnected drag source.");

        public string[] GetFormats() =>
            throw new ExternalException("Disconnected drag source.");

        public void SetData(
            string format,
            object data,
            bool autoConvert) =>
            throw new NotSupportedException();

        public void SetData(string format, object data) =>
            throw new NotSupportedException();

        public void SetData(Type format, object data) =>
            throw new NotSupportedException();

        public void SetData(object data) =>
            throw new NotSupportedException();
    }
}
