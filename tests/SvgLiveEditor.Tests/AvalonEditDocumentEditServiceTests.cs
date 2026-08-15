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

    [TestMethod]
    public void GroupAndUngroupUndoRedoRestoreExactSourceAndLogicalSelection()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><text id=\"b\">سلام</text></svg>";
        SvgDocumentIndex beforeDocument =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgElementNode[] children = beforeDocument.Roots.Single().Children.ToArray();
        SvgMultiSelectionState beforeSelection = new(
            1,
            children.Select(child => child.Identity).ToArray(),
            children[1].Identity,
            children[0].Identity);
        SvgAuthoringEditResult grouped = new SvgGroupService().CreateGroupEdit(
            source,
            beforeDocument,
            children);
        string groupedSource = grouped.Edit!.Apply(source);
        SvgMultiSelectionState groupedSelection = new(
            2,
            grouped.PreferredSelections!,
            grouped.PreferredSelection,
            grouped.PreferredSelection);
        AssertSelectionUndoRoundTrip(
            source,
            groupedSource,
            grouped.Edit,
            beforeSelection,
            groupedSelection);

        SvgDocumentIndex groupedDocument =
            new SvgDocumentIndexService().Build(groupedSource).Document!;
        SvgAuthoringEditResult ungrouped = new SvgGroupService().CreateUngroupEdit(
            groupedSource,
            groupedDocument,
            groupedDocument.Elements.Single(element => element.Name == "g"));
        SvgMultiSelectionState ungroupedSelection = new(
            3,
            ungrouped.PreferredSelections!,
            ungrouped.PreferredSelection,
            ungrouped.PreferredSelection);
        AssertSelectionUndoRoundTrip(
            groupedSource,
            source,
            ungrouped.Edit!,
            groupedSelection,
            ungroupedSelection);
    }

    [TestMethod]
    public void SelectionRestoreTargetRejectsTheWrongSourceAndMalformedDigest()
    {
        SvgMultiSelectionState selection = SvgMultiSelectionState.Empty(1);
        SvgSelectionRestoreTarget target =
            SvgSelectionRestoreTarget.Create(selection, "سلام");
        SvgSelectionRestoreTarget malformed = new(selection, 4, "not-a-hash");

        Assert.IsTrue(target.Matches("سلام"));
        Assert.IsFalse(target.Matches("سلوم"));
        Assert.IsFalse(malformed.Matches("سلام"));
    }

    private static void AssertSelectionUndoRoundTrip(
        string beforeSource,
        string afterSource,
        SourceTextEdit edit,
        SvgMultiSelectionState beforeSelection,
        SvgMultiSelectionState afterSelection)
    {
        TextDocument document = new(beforeSource);
        SvgSelectionRestoreTarget? restored = null;
        SvgSelectionUndoOperation selectionUndo = new(
            SvgSelectionRestoreTarget.Create(beforeSelection, beforeSource),
            SvgSelectionRestoreTarget.Create(afterSelection, afterSource),
            target => restored = target);
        new AvalonEditDocumentEditService().Apply(
            document,
            edit,
            selectionUndo);

        Assert.AreEqual(afterSource, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(beforeSource, document.Text);
        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.Matches(document.Text));
        CollectionAssert.AreEqual(
            beforeSelection.Identities.ToArray(),
            restored.Selection.Identities.ToArray());
        Assert.IsFalse(document.UndoStack.CanUndo);

        restored = null;
        document.UndoStack.Redo();
        Assert.AreEqual(afterSource, document.Text);
        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.Matches(document.Text));
        CollectionAssert.AreEqual(
            afterSelection.Identities.ToArray(),
            restored.Selection.Identities.ToArray());
    }
}
