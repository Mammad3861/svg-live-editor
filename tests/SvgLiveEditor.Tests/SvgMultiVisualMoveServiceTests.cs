using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgMultiVisualMoveServiceTests
{
    private readonly SvgMultiVisualMoveService _service = new();

    [TestMethod]
    public void MixedBasicShapesMoveAtomicallyWithOneUndoUnit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\"><rect x=\"1\" y=\"2\" width=\"10\" height=\"20\"/><circle cx=\"30\" cy=\"40\" r=\"5\"/><ellipse cx=\"60\" cy=\"70\" rx=\"8\" ry=\"9\"/><line x1=\"80\" y1=\"90\" x2=\"100\" y2=\"110\"/><text x=\"5\" y=\"150\">سلام SVG</text></svg>";
        SvgVisualElement[] elements = BuildVisual(source).Elements
            .Where(element => element.IsMovable)
            .ToArray();

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            elements,
            5,
            -2);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        string candidate = result.Edit.Apply(source);
        StringAssert.Contains(candidate, "x=\"6\" y=\"0\"");
        StringAssert.Contains(candidate, "cx=\"35\" cy=\"38\"");
        StringAssert.Contains(candidate, "cx=\"65\" cy=\"68\"");
        StringAssert.Contains(candidate, "x1=\"85\" y1=\"88\" x2=\"105\" y2=\"108\"");
        StringAssert.Contains(candidate, ">سلام SVG</text>");

        TextDocument document = new(source);
        new AvalonEditDocumentEditService().Apply(document, result.Edit);
        Assert.AreEqual(candidate, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(candidate, document.Text);
    }

    [TestMethod]
    public void UnsafeMemberRejectsTheWholeMoveWithoutPartialOutput()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"1\" y=\"2\" width=\"3\" height=\"4\"/><rect transform=\"translate(2)\" x=\"8\" y=\"9\" width=\"3\" height=\"4\"/></svg>";
        SvgVisualElement[] elements = BuildVisual(source).Elements
            .Where(element => element.SourceElement.Name == "rect")
            .ToArray();

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            elements,
            10,
            10);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
    }

    [TestMethod]
    public void StaleSpansRejectEverySelectedElement()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"1\" y=\"2\" width=\"3\" height=\"4\"/><circle cx=\"10\" cy=\"10\" r=\"2\"/></svg>";
        SvgVisualElement[] elements = BuildVisual(source).Elements
            .Where(element => element.IsMovable)
            .ToArray();
        string changed = source.Replace("x=\"1\"", "x=\"11\"", StringComparison.Ordinal);

        SvgAttributeEditResult result = _service.CreateEdit(
            changed,
            elements,
            2,
            3);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage!, "source changed");
    }

    [TestMethod]
    public void DuplicateSelectionIdentityIsRejected()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"1\" y=\"2\" width=\"3\" height=\"4\"/></svg>";
        SvgVisualElement element = BuildVisual(source).Elements
            .Single(item => item.SourceElement.Name == "rect");

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            [element, element],
            1,
            0);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "duplicate");
    }

    private static SvgVisualDocument BuildVisual(string source)
    {
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        return new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
    }
}
