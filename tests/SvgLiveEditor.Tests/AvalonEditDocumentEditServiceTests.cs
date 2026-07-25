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
}
