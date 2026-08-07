using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayerRenameServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLayerRenameService _service = new();

    [TestMethod]
    [DataRow("گروه فارسی")]
    [DataRow("دایره تست ۱")]
    [DataRow("English layer")]
    [DataRow("Hero گروه فارسی ۱")]
    [DataRow("Design & Review \"A\"")]
    public void FriendlyName_PreservesUnicodeEscapesXmlAndNeverChangesTechnicalId(
        string friendlyName)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/><use href=\"#shape\"/></svg>";
        SvgDocumentIndex document = Build(source);

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "shape"),
            friendlyName);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = Build(candidate);
        SvgElementNode renamed = Find(rebuilt, "shape");

        Assert.AreEqual("shape", renamed.Id);
        StringAssert.Contains(candidate, "href=\"#shape\"");
        Assert.AreEqual(
            friendlyName,
            SvgLayerRenameService.DecodeFriendlyName(
                renamed.FindAttribute(SvgLayerRenameService.AttributeName)
                    ?.RawValue));
        if (friendlyName.Contains('&', StringComparison.Ordinal))
        {
            StringAssert.Contains(candidate, "&amp;");
        }
        if (friendlyName.Contains('"', StringComparison.Ordinal))
        {
            StringAssert.Contains(candidate, "&quot;");
        }
    }

    [TestMethod]
    public void FriendlyName_DisplayPrecedenceAllowsDuplicatesAndKeepsTechnicalMetadata()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"one\" data-name=\"گروه مشترک\"></g><g id=\"two\" data-name=\"گروه مشترک\"></g><circle id=\"dot\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgLayerWorkspace workspace = new SvgLayerWorkspaceService().Build(
            document,
            source);

        SvgLayerItem[] groups = workspace.Roots
            .Where(item => item.Element.Name == "g")
            .ToArray();
        Assert.AreEqual(2, groups.Length);
        Assert.IsTrue(groups.All(item => item.Label == "گروه مشترک"));
        CollectionAssert.AreEquivalent(
            new[] { "g #one", "g #two" },
            groups.Select(item => item.TechnicalLabel).ToArray());
        SvgLayerItem circle = workspace.Roots.Single(item =>
            item.Element.Name == "circle");
        Assert.AreEqual("circle #dot", circle.Label);
        Assert.AreEqual(string.Empty, circle.FriendlyName);
    }

    [TestMethod]
    public void FriendlyName_ClearRemovesOnlyOptionalAttribute()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" data-name=\"قدیمی\" fill=\"red\"/></svg>";
        SvgDocumentIndex document = Build(source);

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "shape"),
            string.Empty);
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" fill=\"red\"/></svg>",
            candidate);
    }

    [TestMethod]
    public void FriendlyName_RejectsControlsLengthLockedAndStaleSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode shape = Find(document, "shape");

        Assert.IsFalse(_service.CreateEdit(
            source,
            document,
            shape,
            "bad\nname").IsSuccess);
        Assert.IsFalse(_service.CreateEdit(
            source,
            document,
            shape,
            "\u0001").IsSuccess);
        Assert.IsFalse(_service.CreateEdit(
            source,
            document,
            shape,
            new string('x', SvgLayerRenameService.MaximumNameLength + 1))
            .IsSuccess);
        Assert.IsFalse(_service.CreateEdit(
            source,
            document,
            shape,
            "نام",
            _ => true).IsSuccess);
        Assert.IsFalse(_service.CreateEdit(
            source.Replace("shape", "changed", StringComparison.Ordinal),
            document,
            shape,
            "نام").IsSuccess);
    }

    [TestMethod]
    public void FriendlyName_IsOneUndoAndRedoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "shape"),
            "دایره تست ۱");
        TextDocument textDocument = new(source);
        textDocument.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        string renamed = textDocument.Text;
        textDocument.UndoStack.Undo();
        Assert.AreEqual(source, textDocument.Text);
        Assert.IsFalse(textDocument.UndoStack.CanUndo);
        textDocument.UndoStack.Redo();
        Assert.AreEqual(renamed, textDocument.Text);
    }

    [TestMethod]
    public void FriendlyName_RoundTripsThroughSaveAutoSaveAndRecovery()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.LayerName.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            const string source =
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"></g></svg>";
            SvgDocumentIndex document = Build(source);
            SvgAuthoringEditResult result = _service.CreateEdit(
                source,
                document,
                Find(document, "group"),
                "گروه فارسی & English");
            string updated = result.Edit!.Apply(source);
            Assert.IsTrue(new SvgValidationService().Validate(updated).IsValid);

            Utf8FileService fileService = new();
            string documentPath = Path.Combine(directory, "document.svg");
            fileService.WriteAllText(documentPath, updated);
            Assert.AreEqual(updated, fileService.ReadAllText(documentPath));

            AutoSavePrepareResult prepared =
                new AutoSaveFileService().Prepare(documentPath, updated);
            Assert.IsTrue(prepared.Succeeded, prepared.ErrorMessage);
            using (PreparedAutoSave write = prepared.PreparedWrite!)
            {
                Assert.IsTrue(write.Commit().Succeeded);
            }
            Assert.AreEqual(updated, fileService.ReadAllText(documentPath));

            RecoverySnapshotStore recovery = new(
                Path.Combine(directory, "Recovery"),
                fileService,
                new SafeDocumentPathService());
            RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
                RecoverySnapshotStore.CreateSnapshotId(),
                originalPath: null,
                "document.svg",
                updated,
                revision: 2,
                DateTimeOffset.UtcNow);
            Assert.IsTrue(recovery.TryWrite(snapshot).Succeeded);
            Assert.AreEqual(
                updated,
                recovery.LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
                    .Single().Snapshot.Source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private SvgDocumentIndex Build(string source)
    {
        SvgDocumentIndexResult result = _indexService.Build(source);
        Assert.IsTrue(result.Validation.IsValid, result.Validation.Message);
        Assert.IsNotNull(result.Document);
        return result.Document;
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
