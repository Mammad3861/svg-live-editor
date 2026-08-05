using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayerOrderServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLayerOrderService _service = new();

    [TestMethod]
    public void LayerPositionCountsEligibleSiblingsAndNamesTheParent()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g id="stack">
                <rect id="back"/>
                <metadata>ignored</metadata>
                <circle id="middle"/>
                <defs><path id="definition"/></defs>
                <text id="front">سلام</text>
              </g>
            </svg>
            """;
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerPositionInfo info = _service.GetPositionInfo(
            document,
            Find(document, "middle"));

        Assert.IsTrue(info.IsEligible);
        Assert.AreEqual(2, info.Position);
        Assert.AreEqual(3, info.Count);
        Assert.AreEqual("Layer 2 of 3", info.DisplayText);
        Assert.AreEqual("g #stack", info.ParentLabel);
        StringAssert.Contains(info.BoundaryExplanation, "cannot cross");
    }

    [TestMethod]
    public void BoundaryMessagesExplainThatArrangeCannotCrossTheGroup()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"><rect id=\"back\"/><circle id=\"front\"/></g><line id=\"outside\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerOrderAvailability back = _service.GetAvailability(
            document,
            Find(document, "back"),
            SvgLayerOrderCommand.SendBackward);
        SvgLayerOrderAvailability front = _service.GetAvailability(
            document,
            Find(document, "front"),
            SvgLayerOrderCommand.BringForward);

        Assert.IsFalse(back.CanExecute);
        Assert.IsFalse(front.CanExecute);
        StringAssert.Contains(back.UnavailableReason, "backmost");
        StringAssert.Contains(front.UnavailableReason, "frontmost");
        StringAssert.Contains(back.UnavailableReason, "cannot cross parent or group boundaries");
        StringAssert.Contains(front.UnavailableReason, "g #group");
    }

    [TestMethod]
    [DataRow(SvgLayerOrderCommand.BringForward, "two", "one,three,two", "0/2")]
    [DataRow(SvgLayerOrderCommand.SendBackward, "two", "two,one,three", "0/0")]
    [DataRow(SvgLayerOrderCommand.BringToFront, "one", "two,three,one", "0/2")]
    [DataRow(SvgLayerOrderCommand.SendToBack, "three", "three,one,two", "0/0")]
    public void AllArrangeCommandsMoveToTheExactEligiblePosition(
        SvgLayerOrderCommand command,
        string selectedId,
        string expectedOrder,
        string expectedSelectionPath)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"one\"/><circle id=\"two\"/><text id=\"three\">T</text></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, selectedId),
            command);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        SvgDocumentIndex reordered = _indexService.Build(
            result.Edit!.Apply(source)).Document!;
        string actualOrder = string.Join(",", reordered.Roots.Single().Children
            .Select(element => element.Id));
        Assert.AreEqual(expectedOrder, actualOrder);
        Assert.AreEqual(expectedSelectionPath, result.PreferredSelection!.StructuralPath);
    }

    [TestMethod]
    [DataRow("<rect id=\"shape\"/>")]
    [DataRow("<circle id=\"shape\"/>")]
    [DataRow("<ellipse id=\"shape\"/>")]
    [DataRow("<line id=\"shape\"/>")]
    [DataRow("<text id=\"shape\">سلام</text>")]
    [DataRow("<path id=\"shape\" d=\"M0 0L1 1\"/>")]
    [DataRow("<polygon id=\"shape\" points=\"0,0 1,0 1,1\"/>")]
    [DataRow("<polyline id=\"shape\" points=\"0,0 1,1\"/>")]
    public void EverySupportedPaintableElementCanBeReordered(string elementSource)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\">{elementSource}<rect id=\"peer\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "shape"),
            SvgLayerOrderCommand.BringForward);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        Assert.IsTrue(candidate.IndexOf("id=\"peer\"", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"shape\"", StringComparison.Ordinal));
        if (elementSource.Contains("سلام", StringComparison.Ordinal))
        {
            StringAssert.Contains(candidate, "سلام");
        }
    }

    [TestMethod]
    public void BringForward_ReordersExactElementSpansAndPreservesOtherText()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="one" x="1"/><!-- keep -->
              <metadata>unchanged</metadata>
              <circle id="two" cx="2"/>
              <text id="three">سلام</text>
            </svg>
            """;
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode selected = Find(document, "one");

        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            selected,
            SvgLayerOrderCommand.BringForward);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        Assert.IsTrue(candidate.IndexOf("id=\"two\"", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"one\"", StringComparison.Ordinal));
        Assert.IsTrue(candidate.IndexOf("id=\"one\"", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"three\"", StringComparison.Ordinal));
        StringAssert.Contains(candidate, "<!-- keep -->");
        StringAssert.Contains(candidate, "<metadata>unchanged</metadata>");
        StringAssert.Contains(candidate, "سلام");
        Assert.AreEqual("0/2", result.PreferredSelection!.StructuralPath);
    }

    [TestMethod]
    public void SendToBack_ChangesHitTestPaintOrderWithoutChangingIdsOrReferences()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="back" fill="url(#paint)"/>
              <circle id="middle"/>
              <path id="front" d="M0 0L1 1"/>
            </svg>
            """;
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "front"),
            SvgLayerOrderCommand.SendToBack);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        Assert.IsTrue(candidate.IndexOf("id=\"front\"", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"back\"", StringComparison.Ordinal));
        StringAssert.Contains(candidate, "fill=\"url(#paint)\"");
        Assert.AreEqual(1, candidate.Split("id=\"front\"").Length - 1);
    }

    [TestMethod]
    public void CommandsStayWithinSameParentAndSkipDefinitions()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g><rect id="inside"/><circle id="peer"/></g>
              <defs><rect id="definition"/></defs>
              <rect id="outside"/>
            </svg>
            """;
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "inside"),
            SvgLayerOrderCommand.BringToFront);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        Assert.IsTrue(candidate.IndexOf("id=\"peer\"", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"inside\"", StringComparison.Ordinal));
        Assert.IsTrue(candidate.IndexOf("</g>", StringComparison.Ordinal)
            < candidate.IndexOf("id=\"outside\"", StringComparison.Ordinal));
        Assert.IsFalse(_service.GetAvailability(
            document,
            Find(document, "definition"),
            SvgLayerOrderCommand.BringToFront).CanExecute);
    }

    [TestMethod]
    public void BoundaryAndUnsupportedSelectionsProduceNoEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"first\"/><g id=\"group\"/><circle id=\"last\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        Assert.IsFalse(_service.GetAvailability(
            document,
            Find(document, "first"),
            SvgLayerOrderCommand.SendBackward).CanExecute);
        Assert.IsFalse(_service.GetAvailability(
            document,
            Find(document, "last"),
            SvgLayerOrderCommand.BringToFront).CanExecute);
        Assert.IsFalse(_service.GetAvailability(
            document,
            Find(document, "group"),
            SvgLayerOrderCommand.BringForward).CanExecute);
    }

    [TestMethod]
    public void StaleIndexedSpansFailClosed()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"one\"/><circle id=\"two\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerOrderEditResult result = _service.CreateEdit(
            source.Replace("<rect", "<ellipse", StringComparison.Ordinal),
            document,
            Find(document, "one"),
            SvgLayerOrderCommand.BringForward);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
    }

    [TestMethod]
    public void DuplicateIdsUseReturnedStructuralIdentityAfterReorder()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\"/><circle id=\"same\"/><line id=\"other\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode selected = document.Elements.Single(element =>
            element.Name == "rect");
        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            selected,
            SvgLayerOrderCommand.BringForward);
        SvgDocumentIndex rebuilt = _indexService.Build(
            result.Edit!.Apply(source)).Document!;

        SvgElementNode restored = rebuilt.FindBestMatch(
            result.PreferredSelection!)!;
        Assert.AreEqual("rect", restored.Name);
        Assert.AreEqual("0/1", restored.StructuralPath);
    }

    [TestMethod]
    public void AppliedReorderIsOneUndoUnitAndBoundaryCreatesNoEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"one\"/><circle id=\"two\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerOrderEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "one"),
            SvgLayerOrderCommand.BringForward);
        ICSharpCode.AvalonEdit.Document.TextDocument textDocument = new(source);

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        Assert.IsTrue(textDocument.UndoStack.CanUndo);
        textDocument.UndoStack.Undo();

        Assert.AreEqual(source, textDocument.Text);
        Assert.IsFalse(_service.GetAvailability(
            document,
            Find(document, "one"),
            SvgLayerOrderCommand.SendToBack).CanExecute);
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
