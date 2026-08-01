using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualMoveTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgVisualGeometryIndexService _visualIndexService = new();
    private readonly SvgVisualMoveService _moveService = new();

    [TestMethod]
    public void RectMovementPreservesPrecisionAndUnrelatedSource()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <!-- untouched -->
              <rect id='box' x="12.50" y='4px' width="20" height="10" fill="#fff"/>
              <text>سلام فارسی</text>
            </svg>
            """;

        string updated = Move(source, "box", 1, -2);

        const string expected = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <!-- untouched -->
              <rect id='box' x="13.50" y='2px' width="20" height="10" fill="#fff"/>
              <text>سلام فارسی</text>
            </svg>
            """;
        Assert.AreEqual(expected, updated);
    }

    [TestMethod]
    public void CircleEllipseAndLineMovementUpdatesOnlyPositionAttributes()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <circle id="circle" cx="10" cy="20" r="5" />
              <ellipse id="ellipse" cx="30" cy="40" rx="6" ry="7" />
              <line id="line" x1="1" y1="2" x2="3" y2="4" stroke="red" />
            </svg>
            """;

        string circle = Move(source, "circle", 2, 3);
        StringAssert.Contains(
            circle,
            "<circle id=\"circle\" cx=\"12\" cy=\"23\" r=\"5\" />");
        string ellipse = Move(source, "ellipse", -5, 1);
        StringAssert.Contains(
            ellipse,
            "<ellipse id=\"ellipse\" cx=\"25\" cy=\"41\" rx=\"6\" ry=\"7\" />");
        string line = Move(source, "line", 10, -10);
        StringAssert.Contains(
            line,
            "<line id=\"line\" x1=\"11\" y1=\"-8\" x2=\"13\" y2=\"-6\" stroke=\"red\" />");
    }

    [TestMethod]
    public void MissingDefaultPositionIsAddedOnlyOnChangedAxis()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"box\" width=\"20\" height=\"10\" /></svg>";

        string updated = Move(source, "box", 0, 5);

        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"box\" width=\"20\" height=\"10\" y=\"5\"/></svg>",
            updated);
    }

    [TestMethod]
    public void OneVisualMoveCreatesOneUndoAndRedoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><line id=\"line\" x1=\"1\" y1=\"2\" x2=\"3\" y2=\"4\" /></svg>";
        SvgVisualElement element = Find(source, "line");
        SvgAttributeEditResult result =
            _moveService.CreateEdit(source, element, 4, 5);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(
            document,
            result.Edit!);

        const string expected =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><line id=\"line\" x1=\"5\" y1=\"7\" x2=\"7\" y2=\"9\" /></svg>";
        Assert.AreEqual(expected, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        document.UndoStack.Redo();
        Assert.AreEqual(expected, document.Text);
    }

    [TestMethod]
    public void TextMovementUpdatesOnlyXAndYAndIsOneUndoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"10.0\" y=\"20px\" font-size=\"18\" direction=\"rtl\">سلام SVG</text></svg>";
        SvgVisualElement pending = Find(source, "label");
        SvgVisualElement text = new SvgVisualTextMeasurementService().Apply(
            new SvgVisualDocument(
                new SvgVisualViewport(
                    0,
                    0,
                    300,
                    150,
                    SvgPreserveAspectRatio.Default),
                [pending]),
            [new SvgVisualTextMeasurementResult(
                0,
                true,
                new SvgVisualBounds(10, 2, 90, 22))]).Elements.Single();
        SvgAttributeEditResult result =
            _moveService.CreateEdit(source, text, 5, -2);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        TextDocument document = new(source);

        new AvalonEditDocumentEditService().Apply(document, result.Edit!);

        const string expected =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"15.0\" y=\"18px\" font-size=\"18\" direction=\"rtl\">سلام SVG</text></svg>";
        Assert.AreEqual(expected, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(expected, document.Text);
    }

    [TestMethod]
    public void VisualMovePreservesPersianUtf8AndFeedsPersistencePipeline()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"box\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"/><text direction=\"rtl\">سلام! نسخه ۶.</text></svg>";

        string updated = Move(source, "box", 2, 3);
        SvgValidationResult validation =
            new SvgValidationService().Validate(updated);
        RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
            RecoverySnapshotStore.CreateSnapshotId(),
            null,
            "Untitled.svg",
            updated,
            2,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(validation.IsValid, validation.Message);
        Assert.IsTrue(new AutoSavePolicy().Evaluate(validation).CanWrite);
        Assert.AreEqual(updated, snapshot.Source);
        StringAssert.Contains(updated, "سلام! نسخه ۶.");
        CollectionAssert.AreEqual(
            System.Text.Encoding.UTF8.GetBytes(updated),
            System.Text.Encoding.UTF8.GetBytes(snapshot.Source));
    }

    [TestMethod]
    public void StaleSpanAndUnsupportedGeometryAreRejected()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"box\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"/></svg>";
        SvgVisualElement current = Find(source, "box");

        SvgAttributeEditResult stale = _moveService.CreateEdit(
            $"<!-- changed -->{source}",
            current,
            1,
            1);
        SvgVisualElement unsupported = current with
        {
            UnsupportedReason = "unsupported"
        };
        SvgAttributeEditResult blocked = _moveService.CreateEdit(
            source,
            unsupported,
            1,
            1);

        Assert.IsFalse(stale.IsSuccess);
        StringAssert.Contains(stale.ErrorMessage, "source changed");
        Assert.IsFalse(blocked.IsSuccess);
        Assert.AreEqual("unsupported", blocked.ErrorMessage);
    }

    private string Move(
        string source,
        string id,
        double deltaX,
        double deltaY)
    {
        SvgVisualElement element = Find(source, id);
        SvgAttributeEditResult result =
            _moveService.CreateEdit(source, element, deltaX, deltaY);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        return result.Edit.Apply(source);
    }

    private SvgVisualElement Find(string source, string id)
    {
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);
        SvgVisualDocument visual = _visualIndexService.Build(
            index.Document!,
            new SvgCanvasSizeReader().Read(source),
            source);
        return visual.Elements.Single(element =>
            element.SourceElement.Id == id);
    }
}
