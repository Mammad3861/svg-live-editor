using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualAuthoringReparentTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLayerReparentService _service = new();

    [TestMethod]
    public void Reparent_InsideGroupMakesElementFrontmostAndPreservesUtf8()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"move\"/><text>سلام</text><g id=\"target\"><circle id=\"old\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "target"),
            SvgLayerDropPlacement.Inside);
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        SvgElementNode target = Find(rebuilt, "target");
        Assert.AreEqual("move", target.Children.Last().Id);
        StringAssert.Contains(candidate, "سلام");
        Assert.AreEqual("move", rebuilt.FindBestMatch(
            result.PreferredSelection!)!.Id);
    }

    [TestMethod]
    public void Reparent_SupportsSelfClosingEmptyGroupAndMoveBackToRoot()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"move\"/><g id=\"target\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAuthoringEditResult inside = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "target"),
            SvgLayerDropPlacement.Inside);
        string grouped = inside.Edit!.Apply(source);
        SvgDocumentIndex groupedDocument = _indexService.Build(grouped).Document!;

        Assert.IsTrue(inside.IsSuccess, inside.ErrorMessage);
        Assert.AreEqual("move", Find(groupedDocument, "target").Children.Single().Id);

        SvgAuthoringEditResult root = _service.CreateMoveToRootFrontEdit(
            grouped,
            groupedDocument,
            Find(groupedDocument, "move"));
        string movedOut = root.Edit!.Apply(grouped);
        SvgDocumentIndex rootDocument = _indexService.Build(movedOut).Document!;
        Assert.IsTrue(root.IsSuccess, root.ErrorMessage);
        Assert.AreEqual("move", rootDocument.Roots.Single().Children.Last().Id);
        Assert.AreEqual(0, Find(rootDocument, "target").Children.Count);
    }

    [TestMethod]
    public void Reparent_BeforeAfterUsesTopmostFirstConventionAcrossParents()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"from\"><rect id=\"move\"/></g><g id=\"to\"><circle id=\"back\"/><line id=\"front\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult before = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "front"),
            SvgLayerDropPlacement.Before);
        SvgDocumentIndex rebuilt = _indexService.Build(
            before.Edit!.Apply(source)).Document!;

        Assert.IsTrue(before.IsSuccess, before.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "back", "front", "move" },
            Find(rebuilt, "to").Children.Select(child => child.Id).ToArray());
    }

    [TestMethod]
    public void Reparent_SameParentRetainsExistingOrderingSemantics()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"back\"/><circle id=\"front\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateDropEdit(
            source,
            document,
            Find(document, "back"),
            Find(document, "front"),
            SvgLayerDropPlacement.Before);
        SvgDocumentIndex rebuilt = _indexService.Build(
            result.Edit!.Apply(source)).Document!;

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "front", "back" },
            rebuilt.Roots.Single().Children.Select(child => child.Id).ToArray());
    }

    [TestMethod]
    public void Reparent_RejectsCyclesInvalidTargetsLocksAndStaleSpans()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"outer\"><g id=\"inner\"></g></g><rect id=\"peer\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode outer = Find(document, "outer");
        SvgElementNode inner = Find(document, "inner");
        SvgElementNode peer = Find(document, "peer");

        Assert.IsFalse(_service.CreateDropEdit(
            source,
            document,
            outer,
            inner,
            SvgLayerDropPlacement.Inside).IsSuccess);
        Assert.IsFalse(_service.CreateDropEdit(
            source,
            document,
            peer,
            peer,
            SvgLayerDropPlacement.Inside).IsSuccess);
        Assert.IsFalse(_service.CreateDropEdit(
            source,
            document,
            peer,
            inner,
            SvgLayerDropPlacement.Inside,
            _ => true).IsSuccess);
        Assert.IsFalse(_service.CreateDropEdit(
            source.Replace("<rect", "<line", StringComparison.Ordinal),
            document,
            peer,
            inner,
            SvgLayerDropPlacement.Inside).IsSuccess);
    }

    [TestMethod]
    public void Reparent_RejectsLockedSourceAndLockedDestinationSeparately()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"move\"/><g id=\"target\"></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode moving = Find(document, "move");
        SvgElementNode target = Find(document, "target");

        SvgAuthoringEditResult lockedSource = _service.CreateDropEdit(
            source,
            document,
            moving,
            target,
            SvgLayerDropPlacement.Inside,
            element => ReferenceEquals(element, moving));
        SvgAuthoringEditResult lockedTarget = _service.CreateDropEdit(
            source,
            document,
            moving,
            target,
            SvgLayerDropPlacement.Inside,
            element => ReferenceEquals(element, target));

        Assert.IsFalse(lockedSource.IsSuccess);
        Assert.IsNull(lockedSource.Edit);
        Assert.IsFalse(lockedTarget.IsSuccess);
        Assert.IsNull(lockedTarget.Edit);
    }

    [TestMethod]
    public void Reparent_PreservesElementOwnedVisibilityAttribute()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"move\" display=\"none\"/><g id=\"target\"></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "target"),
            SvgLayerDropPlacement.Inside);
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        StringAssert.Contains(candidate, "<rect id=\"move\" display=\"none\"/>");
    }

    [TestMethod]
    [DataRow("transform=\"translate(10)\"")]
    [DataRow("style=\"fill:red\"")]
    [DataRow("opacity=\"0.5\"")]
    [DataRow("clip-path=\"url(#clip)\"")]
    [DataRow("font-family=\"Segoe UI\"")]
    public void Reparent_RejectsInheritedSemanticContextChanges(
        string groupAttribute)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"from\" {groupAttribute}><rect id=\"move\"/></g><g id=\"to\"></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "to"),
            SvgLayerDropPlacement.Inside);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage, "could change inherited");
    }

    [TestMethod]
    public void Reparent_IsExactlyOneUndoAndRedoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"from\"><rect id=\"move\"/></g><g id=\"to\"></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAuthoringEditResult result = _service.CreateDropEdit(
            source,
            document,
            Find(document, "move"),
            Find(document, "to"),
            SvgLayerDropPlacement.Inside);
        TextDocument textDocument = new(source);
        textDocument.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        string moved = textDocument.Text;
        textDocument.UndoStack.Undo();
        Assert.AreEqual(source, textDocument.Text);
        Assert.IsFalse(textDocument.UndoStack.CanUndo);
        textDocument.UndoStack.Redo();
        Assert.AreEqual(moved, textDocument.Text);
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
