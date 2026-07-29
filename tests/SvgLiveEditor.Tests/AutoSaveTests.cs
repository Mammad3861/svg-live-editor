using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class AutoSaveTests
{
    private string _directory = null!;
    private AutoSaveFileService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.AutoSave.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _service = new AutoSaveFileService();
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (string file in Directory.GetFiles(_directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public void PreparedWrite_DoesNotTouchOriginalUntilAtomicCommit()
    {
        string path = Path.Combine(_directory, "document.svg");
        const string original =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>old</text></svg>";
        const string updated =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام جدید</text></svg>";
        new Utf8FileService().WriteAllText(path, original);

        AutoSavePrepareResult result = _service.Prepare(path, updated);
        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        Assert.AreEqual(original, new Utf8FileService().ReadAllText(path));

        using PreparedAutoSave write = result.PreparedWrite!;
        PersistenceOperationResult commit = write.Commit();

        Assert.IsTrue(commit.Succeeded, commit.ErrorMessage);
        Assert.AreEqual(updated, new Utf8FileService().ReadAllText(path));
        CollectionAssert.AreEqual(
            System.Text.Encoding.UTF8.GetBytes(updated),
            File.ReadAllBytes(path));
        Assert.IsFalse(Directory.GetFiles(
            _directory,
            ".SvgLiveEditor-*.tmp").Any());
    }

    [TestMethod]
    public void DisposingPreparedWrite_LeavesOriginalUntouched()
    {
        string path = Path.Combine(_directory, "cancelled.txt");
        File.WriteAllText(path, "original");

        AutoSavePrepareResult result =
            _service.Prepare(path, "staged");
        Assert.IsTrue(result.Succeeded, result.ErrorMessage);

        result.PreparedWrite!.Dispose();

        Assert.AreEqual("original", File.ReadAllText(path));
        Assert.IsFalse(Directory.GetFiles(
            _directory,
            ".SvgLiveEditor-*.tmp").Any());
    }

    [TestMethod]
    public void MissingAndReadOnlyOriginals_AreNeverRecreatedOrOverwritten()
    {
        string missing = Path.Combine(_directory, "missing.svg");
        AutoSavePrepareResult missingResult =
            _service.Prepare(missing, "new");
        Assert.IsFalse(missingResult.Succeeded);
        Assert.IsFalse(File.Exists(missing));

        string readOnly = Path.Combine(_directory, "readonly.svg");
        File.WriteAllText(readOnly, "original");
        File.SetAttributes(
            readOnly,
            File.GetAttributes(readOnly) | FileAttributes.ReadOnly);
        AutoSavePrepareResult readOnlyResult =
            _service.Prepare(readOnly, "changed");

        Assert.IsFalse(readOnlyResult.Succeeded);
        Assert.AreEqual("original", File.ReadAllText(readOnly));
    }

    [TestMethod]
    public void InvalidSvgPolicy_PausesWithoutPreparingAWrite()
    {
        SvgValidationResult invalid =
            new SvgValidationService().Validate(
                "<svg xmlns=\"http://www.w3.org/2000/svg\">");

        AutoSaveValidationDecision decision =
            new AutoSavePolicy().Evaluate(invalid);

        Assert.IsFalse(decision.CanWrite);
        Assert.AreEqual(
            "Auto Save paused · Invalid SVG",
            decision.StatusMessage);
    }

    [TestMethod]
    public void ValidSvgPolicy_EnablesAtomicWrite()
    {
        SvgValidationResult valid =
            new SvgValidationService().Validate(
                "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        AutoSaveValidationDecision decision =
            new AutoSavePolicy().Evaluate(valid);

        Assert.IsTrue(decision.CanWrite);
        Assert.AreEqual("Auto-saving...", decision.StatusMessage);
    }

    [TestMethod]
    public void UnencodableSource_FailsWithoutTouchingOriginal()
    {
        string path = Path.Combine(_directory, "invalid-utf16.svg");
        File.WriteAllText(path, "original");
        string unpairedSurrogate = "\uD800";

        AutoSavePrepareResult result =
            _service.Prepare(path, unpairedSurrogate);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("original", File.ReadAllText(path));
    }

    [TestMethod]
    public void NamedTxtContainingValidSvgAutoSavesExactUtf8()
    {
        string path = Path.Combine(_directory, "document.txt");
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>متن فارسی</text></svg>\r\n";
        File.WriteAllText(path, "old");

        AutoSavePrepareResult prepared =
            _service.Prepare(path, source);
        Assert.IsTrue(prepared.Succeeded, prepared.ErrorMessage);
        using PreparedAutoSave write = prepared.PreparedWrite!;
        Assert.IsTrue(write.Commit().Succeeded);

        Assert.AreEqual(source, new Utf8FileService().ReadAllText(path));
    }

    [TestMethod]
    public void AutoSavePreservesMixedPersianPunctuationExactly()
    {
        string path = Path.Combine(_directory, "persian.svg");
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text direction=\"rtl\" unicode-bidi=\"embed\" text-anchor=\"start\">نسخه 2.0 آماده است.</text></svg>";
        File.WriteAllText(path, "old");

        AutoSavePrepareResult prepared = _service.Prepare(path, source);
        Assert.IsTrue(prepared.Succeeded, prepared.ErrorMessage);
        using PreparedAutoSave write = prepared.PreparedWrite!;
        Assert.IsTrue(write.Commit().Succeeded);

        Assert.AreEqual(source, new Utf8FileService().ReadAllText(path));
        CollectionAssert.AreEqual(
            System.Text.Encoding.UTF8.GetBytes(source),
            File.ReadAllBytes(path));
    }

    [TestMethod]
    public void ExactCurrentSaveStateDoesNotChangeDocumentTextOrPath()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>exact</text></svg>";
        string path = Path.Combine(_directory, "state.svg");
        MainViewModel viewModel = new();
        viewModel.LoadDocument(source, path);
        viewModel.UpdateTextFromEditor(source + "\r\n");
        Assert.IsTrue(viewModel.IsModified);

        viewModel.MarkSaved(path);

        Assert.IsFalse(viewModel.IsModified);
        Assert.AreEqual(source + "\r\n", viewModel.DocumentText);
        Assert.AreEqual(path, viewModel.CurrentFilePath);
    }
}
