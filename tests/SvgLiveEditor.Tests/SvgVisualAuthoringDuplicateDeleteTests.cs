using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualAuthoringDuplicateDeleteTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgElementDuplicateService _duplicateService = new();
    private readonly SvgElementDeleteService _deleteService = new();

    [TestMethod]
    public void Duplicate_InsertsFrontOfOriginalAndGeneratesDeterministicUniqueId()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"item-copy\"/><circle id=\"item\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _duplicateService.CreateEdit(
            source,
            document,
            Find(document, "item"));
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "item-copy", "item", "item-copy-2" },
            rebuilt.Roots.Single().Children.Select(element => element.Id).ToArray());
        Assert.AreEqual("item-copy-2", rebuilt.FindBestMatch(
            result.PreferredSelection!)!.Id);
    }

    [TestMethod]
    public void Duplicate_RemapsSubtreeIdsAndKnownInternalReferences()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"card\"><defs><linearGradient id=\"paint\"/></defs><rect id=\"shape\" fill=\"url(#paint)\"/><use href=\"#shape\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _duplicateService.CreateEdit(
            source,
            document,
            Find(document, "card"));
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        StringAssert.Contains(candidate, "id=\"card-copy\"");
        StringAssert.Contains(candidate, "id=\"paint-copy\"");
        StringAssert.Contains(candidate, "id=\"shape-copy\"");
        StringAssert.Contains(candidate, "fill=\"url(#paint-copy)\"");
        StringAssert.Contains(candidate, "href=\"#shape-copy\"");
        Assert.AreEqual(1, candidate.Split("id=\"paint-copy\"").Length - 1);
    }

    [TestMethod]
    public void Duplicate_RejectsAmbiguousOrMissingSubtreeReferences()
    {
        const string ambiguous =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><defs><linearGradient id=\"paint\"/><linearGradient id=\"paint\"/></defs><g id=\"card\"><rect fill=\"url(#paint)\"/></g></svg>";
        SvgDocumentIndex ambiguousDocument = _indexService.Build(ambiguous).Document!;
        SvgAuthoringEditResult ambiguousResult = _duplicateService.CreateEdit(
            ambiguous,
            ambiguousDocument,
            Find(ambiguousDocument, "card"));

        const string missing =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"card\"><rect fill=\"url(#missing)\"/></g></svg>";
        SvgDocumentIndex missingDocument = _indexService.Build(missing).Document!;
        SvgAuthoringEditResult missingResult = _duplicateService.CreateEdit(
            missing,
            missingDocument,
            Find(missingDocument, "card"));

        Assert.IsFalse(ambiguousResult.IsSuccess);
        StringAssert.Contains(ambiguousResult.ErrorMessage, "ambiguous ID 'paint'");
        Assert.IsFalse(missingResult.IsSuccess);
        StringAssert.Contains(missingResult.ErrorMessage, "missing or ambiguous ID 'missing'");
    }

    [TestMethod]
    public void Duplicate_RejectsInternalReferenceWhenTargetIdIsNotGloballyUnique()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/><g id=\"card\"><rect id=\"shape\"/><use href=\"#shape\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _duplicateService.CreateEdit(
            source,
            document,
            Find(document, "card"));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage, "ambiguous ID 'shape'");
    }

    [TestMethod]
    public void Duplicate_RejectsLockedAndDefinitionElements()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><defs><path id=\"definition\"/></defs><rect id=\"locked\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode locked = Find(document, "locked");

        Assert.IsFalse(_duplicateService.CreateEdit(
            source,
            document,
            locked,
            _ => true).IsSuccess);
        Assert.IsFalse(_duplicateService.CreateEdit(
            source,
            document,
            Find(document, "definition")).IsSuccess);
    }

    [TestMethod]
    public void DuplicateAndDeleteRejectSameLengthStaleAttributeSpans()
    {
        const string indexedSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"one\"/></svg>";
        const string changedSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"two\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(indexedSource).Document!;
        SvgElementNode staleElement = Find(document, "one");

        SvgAuthoringEditResult duplicate = _duplicateService.CreateEdit(
            changedSource,
            document,
            staleElement);
        SvgAuthoringEditResult delete = _deleteService.CreateEdit(
            changedSource,
            document,
            staleElement);

        Assert.IsFalse(duplicate.IsSuccess);
        Assert.IsNull(duplicate.Edit);
        Assert.IsFalse(delete.IsSuccess);
        Assert.IsNull(delete.Edit);
    }

    [TestMethod]
    public void Duplicate_PreservesSourceOwnedVisibilityWithoutInventingState()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"hidden\" display=\"none\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _duplicateService.CreateEdit(
            source,
            document,
            Find(document, "hidden"));
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(
            2,
            candidate.Split("display=\"none\"", StringSplitOptions.None).Length - 1);
        StringAssert.Contains(candidate, "id=\"hidden-copy\"");
    }

    [TestMethod]
    public void Delete_RemovesExactSpanPreservesUtf8AndSelectsNeighbor()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"before\">سلام</text><!-- keep --><rect id=\"gone\"/><circle id=\"after\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _deleteService.CreateEdit(
            source,
            document,
            Find(document, "gone"));
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsFalse(candidate.Contains("id=\"gone\"", StringComparison.Ordinal));
        StringAssert.Contains(candidate, "سلام");
        StringAssert.Contains(candidate, "<!-- keep -->");
        Assert.AreEqual("after", result.PreferredSelection!.Id);
    }

    [TestMethod]
    public void Delete_BlocksKnownExternalReferenceAndNeverDeletesRoot()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/><use href=\"#shape\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult referenced = _deleteService.CreateEdit(
            source,
            document,
            Find(document, "shape"));
        SvgAuthoringEditResult root = _deleteService.CreateEdit(
            source,
            document,
            document.Roots.Single());

        Assert.IsFalse(referenced.IsSuccess);
        Assert.IsNull(referenced.Edit);
        StringAssert.Contains(referenced.ErrorMessage, "refers to ID 'shape'");
        Assert.IsFalse(root.IsSuccess);
    }

    [TestMethod]
    public void Delete_NonEmptyGroupRequiresConfirmationButEmptyGroupDoesNot()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"full\"><rect/></g><g id=\"textual\">سلام<!-- keep --></g><g id=\"empty\"> \r\n </g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult full = _deleteService.CreateEdit(
            source,
            document,
            Find(document, "full"));
        SvgAuthoringEditResult empty = _deleteService.CreateEdit(
            source,
            document,
            Find(document, "empty"));
        SvgAuthoringEditResult textual = _deleteService.CreateEdit(
            source,
            document,
            Find(document, "textual"));

        Assert.IsTrue(full.IsSuccess);
        Assert.IsTrue(full.RequiresConfirmation);
        StringAssert.Contains(full.ConfirmationMessage, "1 descendant");
        Assert.IsTrue(empty.IsSuccess);
        Assert.IsFalse(empty.RequiresConfirmation);
        Assert.IsTrue(textual.IsSuccess);
        Assert.IsTrue(textual.RequiresConfirmation);
        StringAssert.Contains(textual.ConfirmationMessage, "all of its contents");
    }

    [TestMethod]
    public void DuplicateAndDeleteAreEachOneUndoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        foreach (SvgAuthoringEditResult result in new[]
        {
            _duplicateService.CreateEdit(source, document, Find(document, "shape")),
            _deleteService.CreateEdit(source, document, Find(document, "shape"))
        })
        {
            TextDocument textDocument = new(source);
            textDocument.UndoStack.MarkAsOriginalFile();
            new AvalonEditDocumentEditService().Apply(
                textDocument,
                result.Edit!);
            string changed = textDocument.Text;
            textDocument.UndoStack.Undo();
            Assert.AreEqual(source, textDocument.Text);
            Assert.IsFalse(textDocument.UndoStack.CanUndo);
            textDocument.UndoStack.Redo();
            Assert.AreEqual(changed, textDocument.Text);
        }
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
