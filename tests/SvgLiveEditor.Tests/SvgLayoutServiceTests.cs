using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayoutServiceTests
{
    private readonly SvgLayoutService _service = new();

    [TestMethod]
    [DataRow(SvgLayoutCommand.AlignLeft, "left", 10d)]
    [DataRow(SvgLayoutCommand.AlignHorizontalCenters, "centerX", 50d)]
    [DataRow(SvgLayoutCommand.AlignRight, "right", 90d)]
    [DataRow(SvgLayoutCommand.AlignTop, "top", 20d)]
    [DataRow(SvgLayoutCommand.AlignVerticalCenters, "centerY", 50d)]
    [DataRow(SvgLayoutCommand.AlignBottom, "bottom", 80d)]
    public void AllAlignmentCommandsUseValidatedVisualBounds(
        SvgLayoutCommand command,
        string measure,
        double expected)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\"><rect id=\"a\" x=\"10\" y=\"20\" width=\"20\" height=\"20\"/><rect id=\"b\" x=\"50\" y=\"60\" width=\"40\" height=\"20\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements,
            command);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        string candidate = result.Edit.Apply(source);
        SvgVisualElement[] aligned = Build(candidate).Elements;
        foreach (SvgVisualElement element in aligned)
        {
            SvgVisualBounds bounds = element.Geometry!.Bounds;
            double actual = measure switch
            {
                "left" => bounds.Left,
                "centerX" => (bounds.Left + bounds.Right) / 2,
                "right" => bounds.Right,
                "top" => bounds.Top,
                "centerY" => (bounds.Top + bounds.Bottom) / 2,
                "bottom" => bounds.Bottom,
                _ => throw new InvalidOperationException()
            };
            Assert.AreEqual(expected, actual, 0.0001);
        }
    }

    [TestMethod]
    public void AlignmentSupportsDirectTextOnlyWhenMeasuredBoundsAreProvided()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><text x=\"40\" y=\"30\">Hi</text></svg>";
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
        SvgVisualElement rectangle = visual.Elements
            .Single(element => element.Kind == SvgVisualElementKind.Rect);
        SvgElementNode textNode = document.Elements
            .Single(element => element.Name == "text");
        SvgVisualElement measuredText = new(
            textNode,
            SvgVisualElementKind.Text,
            new SvgVisualShapeGeometry(
                SvgVisualElementKind.Text,
                40,
                10,
                60,
                30),
            UnsupportedReason: null);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            [rectangle, measuredText],
            SvgLayoutCommand.AlignLeft);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        StringAssert.Contains(result.Edit!.Apply(source), "<text x=\"10\"");
    }

    [TestMethod]
    public void IncompatibleParentsRejectWholeLayout()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><rect id=\"a\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/></g><g><rect id=\"b\" x=\"30\" y=\"0\" width=\"10\" height=\"10\"/></g></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);
        SvgAttributeEditResult crossParent = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.AlignTop);
        Assert.IsFalse(crossParent.IsSuccess);
        Assert.IsNull(crossParent.Edit);
        Assert.AreEqual(
            "The selected elements must share a compatible parent coordinate system.",
            crossParent.ErrorMessage);
        TextDocument unchanged = new(source);
        Assert.IsFalse(unchanged.UndoStack.CanUndo);
    }

    [TestMethod]
    public void OneLockedSameParentMemberRejectsTheCompleteLayout()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"open\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"locked\" x=\"30\" y=\"20\" width=\"10\" height=\"10\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.AlignTop,
            element => element.Id == "locked");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        Assert.AreEqual(
            "The selection contains a locked element or locked ancestor.",
            result.ErrorMessage);
    }

    [TestMethod]
    public void OneHiddenMemberRejectsTheCompleteLayout()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shown\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"hidden\" x=\"30\" y=\"20\" width=\"10\" height=\"10\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) = Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.AlignTop,
            isEffectivelyLocked: null,
            isEffectivelyVisible: element => element.Id != "hidden");

        Assert.IsNull(result.Edit);
        Assert.AreEqual(
            "The selection contains a hidden element.",
            result.ErrorMessage);
    }

    [TestMethod]
    public void HorizontalDistributionUsesEqualVisualGapsAndKeepsOuterAnchors()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"left\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"middle\" x=\"30\" y=\"0\" width=\"20\" height=\"10\"/><rect id=\"right\" x=\"100\" y=\"0\" width=\"30\" height=\"10\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements.Reverse().ToArray(),
            SvgLayoutCommand.DistributeHorizontally);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        SvgVisualElement[] distributed = Build(candidate).Elements
            .OrderBy(element => element.Geometry!.Bounds.Left)
            .ToArray();
        Assert.AreEqual(0, distributed[0].Geometry!.Bounds.Left);
        Assert.AreEqual(45, distributed[1].Geometry!.Bounds.Left);
        Assert.AreEqual(130, distributed[2].Geometry!.Bounds.Right);
        SvgVisualBounds first = distributed[0].Geometry!.Bounds;
        SvgVisualBounds middle = distributed[1].Geometry!.Bounds;
        SvgVisualBounds last = distributed[2].Geometry!.Bounds;
        double firstGap = middle.Left - first.Right;
        double secondGap = last.Left - middle.Right;
        Assert.AreEqual(firstGap, secondGap, 0.0001);
        AssertSingleUndo(source, candidate, result.Edit);
    }

    [TestMethod]
    public void VerticalDistributionIsDeterministicForUnequalDimensions()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"top\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"middle\" x=\"0\" y=\"20\" width=\"10\" height=\"20\"/><rect id=\"bottom\" x=\"0\" y=\"90\" width=\"10\" height=\"30\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);
        SvgAttributeEditResult forward = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.DistributeVertically);
        SvgAttributeEditResult reverse = _service.CreateEdit(
            source,
            document,
            elements.Reverse().ToArray(),
            SvgLayoutCommand.DistributeVertically);

        Assert.IsTrue(forward.IsSuccess, forward.ErrorMessage);
        Assert.AreEqual(
            forward.Edit!.Apply(source),
            reverse.Edit!.Apply(source));
        StringAssert.Contains(forward.Edit.Apply(source), "id=\"middle\" x=\"0\" y=\"40\"");
    }

    [TestMethod]
    public void AlreadyDistributedSelectionProducesNoEditOrUndoUnit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect x=\"45\" y=\"0\" width=\"20\" height=\"10\"/><rect x=\"100\" y=\"0\" width=\"30\" height=\"10\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.DistributeHorizontally);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNull(result.Edit);
    }

    [TestMethod]
    public void AlignmentBoundaryProducesNoEditOrUndoUnit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"0\" y=\"10\" width=\"10\" height=\"10\"/><rect x=\"30\" y=\"10\" width=\"20\" height=\"20\"/></svg>";
        (SvgDocumentIndex document, SvgVisualElement[] elements) =
            Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            elements,
            SvgLayoutCommand.AlignTop);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNull(result.Edit);
    }

    private static (SvgDocumentIndex Document, SvgVisualElement[] Elements)
        Build(string source)
    {
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
        return (document, visual.Elements
            .Where(element => element.SourceElement.Name is
                "rect" or "circle" or "ellipse" or "line")
            .ToArray());
    }

    private static void AssertSingleUndo(
        string source,
        string candidate,
        SourceTextEdit edit)
    {
        TextDocument document = new(source);
        new AvalonEditDocumentEditService().Apply(document, edit);
        Assert.AreEqual(candidate, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
    }
}
