using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayerVisibilityServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLayerVisibilityService _service = new();

    [TestMethod]
    public void HideAddsOnlyDisplayNoneAndShowRemovesOwnedAttribute()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\">سلام</text></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode text = Find(document, "label");

        SvgLayerVisibilityEditResult hidden = _service.CreateEdit(
            source,
            document,
            text,
            ownsHiddenAttribute: false);
        string hiddenSource = hidden.Edit!.Apply(source);
        SvgDocumentIndex hiddenDocument =
            _indexService.Build(hiddenSource).Document!;
        SvgLayerVisibilityEditResult shown = _service.CreateEdit(
            hiddenSource,
            hiddenDocument,
            Find(hiddenDocument, "label"),
            ownsHiddenAttribute: true);

        Assert.IsTrue(hidden.IsSuccess, hidden.ErrorMessage);
        StringAssert.Contains(hiddenSource, "display=\"none\"");
        StringAssert.Contains(hiddenSource, "سلام");
        Assert.IsTrue(shown.IsSuccess, shown.ErrorMessage);
        Assert.AreEqual(source, shown.Edit!.Apply(hiddenSource));
        Assert.IsFalse(shown.OwnsHiddenAttributeAfterEdit);
    }

    [TestMethod]
    [DataRow("display=\"inline\"", "display")]
    [DataRow("display=\"none\"", "display")]
    [DataRow("visibility=\"hidden\"", "visibility")]
    [DataRow("style=\"display:none\"", "style")]
    public void AuthoredVisibilityOwnershipIsNeverOverwritten(
        string attribute,
        string expectedReason)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" {attribute}/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode element = Find(document, "shape");

        SvgLayerVisibilityState state = _service.Analyze(
            document,
            element,
            ownsHiddenAttribute: false);
        SvgLayerVisibilityEditResult result = _service.CreateEdit(
            source,
            document,
            element,
            ownsHiddenAttribute: false);

        Assert.IsFalse(state.CanToggle);
        StringAssert.Contains(state.UnavailableReason, expectedReason);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
    }

    [TestMethod]
    public void AnimatedDisplayIsRejected()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"><set attributeName=\"display\" to=\"none\"/></rect></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerVisibilityState state = _service.Analyze(
            document,
            Find(document, "shape"),
            ownsHiddenAttribute: false);

        Assert.IsFalse(state.CanToggle);
        StringAssert.Contains(state.UnavailableReason, "Animated");
    }

    [TestMethod]
    public void StaleSpanIsRejectedWithoutAnEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgLayerVisibilityEditResult result = _service.CreateEdit(
            source.Replace("rect", "circle", StringComparison.Ordinal),
            document,
            Find(document, "shape"),
            ownsHiddenAttribute: false);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
    }

    [TestMethod]
    public void VisibilityEditIsOneUndoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerVisibilityEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "shape"),
            ownsHiddenAttribute: false);
        ICSharpCode.AvalonEdit.Document.TextDocument textDocument = new(source);

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        Assert.IsTrue(textDocument.UndoStack.CanUndo);
        textDocument.UndoStack.Undo();
        Assert.AreEqual(source, textDocument.Text);
        textDocument.UndoStack.Redo();
        StringAssert.Contains(textDocument.Text, "display=\"none\"");
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
