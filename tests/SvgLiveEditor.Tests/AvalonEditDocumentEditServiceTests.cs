using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class AvalonEditDocumentEditServiceTests
{
    [TestMethod]
    public void Apply_CreatesOneLogicalUndoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"red\" /></svg>";
        SvgDocumentIndex index = new SvgDocumentIndexService().Build(source).Document!;
        SvgElementNode rectangle = index.Elements.Single(element => element.Name == "rect");
        SvgAttributeEditResult result = new SvgAttributeEditService().CreateEdit(
            source,
            rectangle,
            "fill",
            "blue");
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(document, result.Edit!);

        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"blue\" /></svg>",
            document.Text);
        Assert.IsTrue(document.UndoStack.CanUndo);

        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);

        document.UndoStack.Redo();
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"blue\" /></svg>",
            document.Text);
    }

    [TestMethod]
    public void DirectionPropertyEdit_IsOneLogicalUndoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text direction=\"ltr\">سلام! من بهروز هستم.</text></svg>";
        SvgElementNode text = new SvgDocumentIndexService()
            .Build(source)
            .Document!
            .Elements
            .Single(element => element.Name == "text");
        SvgAttributeEditResult result =
            new SvgAttributeEditService().CreateEdit(
                source,
                text,
                "direction",
                "rtl");
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(document, result.Edit!);

        const string expected =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text direction=\"rtl\">سلام! من بهروز هستم.</text></svg>";
        Assert.AreEqual(expected, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        document.UndoStack.Redo();
        Assert.AreEqual(expected, document.Text);
    }
}
