using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SourceSynchronizationFoundationTests
{
    private const string MixedSource =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\">\u0633\u0644\u0627\u0645 English</text><rect id=\"shape\" fill=\"red\"/></svg>";

    [TestMethod]
    public void LatestFocusedSourceEditAloneCanSynchronizeTheCaret()
    {
        SourceRevisionTracker tracker = new();
        long sourceRevision = tracker.Advance(SourceChangeOrigin.SourceEditor);

        Assert.IsTrue(tracker.CanSynchronizeSourceCaret(
            sourceRevision,
            sourceEditorHasKeyboardFocus: true,
            isTextCompositionActive: false,
            selectionRestoreApplied: false));
        Assert.IsFalse(tracker.CanSynchronizeSourceCaret(
            sourceRevision - 1,
            sourceEditorHasKeyboardFocus: true,
            isTextCompositionActive: false,
            selectionRestoreApplied: false));
        Assert.IsFalse(tracker.CanSynchronizeSourceCaret(
            sourceRevision,
            sourceEditorHasKeyboardFocus: false,
            isTextCompositionActive: false,
            selectionRestoreApplied: false));
        Assert.IsFalse(tracker.CanSynchronizeSourceCaret(
            sourceRevision,
            sourceEditorHasKeyboardFocus: true,
            isTextCompositionActive: true,
            selectionRestoreApplied: false));
        Assert.IsFalse(tracker.CanSynchronizeSourceCaret(
            sourceRevision,
            sourceEditorHasKeyboardFocus: true,
            isTextCompositionActive: false,
            selectionRestoreApplied: true));
    }

    [TestMethod]
    public void NonSourceOriginsNeverImpersonateUserSourceTyping()
    {
        foreach (SourceChangeOrigin origin in new[]
                 {
                     SourceChangeOrigin.InspectorProperty,
                     SourceChangeOrigin.VisualCommand,
                     SourceChangeOrigin.UndoRedo,
                     SourceChangeOrigin.DocumentLoad
                 })
        {
            SourceRevisionTracker tracker = new();
            long revision = tracker.Advance(origin);

            Assert.AreEqual(origin, tracker.CurrentOrigin);
            Assert.IsFalse(tracker.CanSynchronizeSourceCaret(
                revision,
                sourceEditorHasKeyboardFocus: true,
                isTextCompositionActive: false,
                selectionRestoreApplied: false));
        }
    }

    [TestMethod]
    public void SourceCaretSyncUpdatesOnlyInspectionAndCannotReenterSourceNavigation()
    {
        TextDocument source = new(MixedSource);
        source.UndoStack.MarkAsOriginalFile();
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(MixedSource).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: MixedSource);
        SvgElementNode text = index.Elements.Single(element =>
            element.Id == "label");
        int selectionStart = MixedSource.IndexOf("\u0633\u0644\u0627\u0645", StringComparison.Ordinal);
        const int selectionLength = 4;

        inspector.SelectNode(text, InspectorSelectionOrigin.SourceCaretSync);
        InspectorSelectionOrigin origin =
            inspector.SelectedElement!.ConsumePendingSelectionOrigin()
            ?? InspectorSelectionOrigin.InspectorRestore;
        bool navigated = new InspectorSelectionCoordinator().TryGetNavigationSpan(
            origin,
            text.StartTagSpan,
            isIndexCurrent: true,
            indexRevision: 1,
            sourceRevision: 1,
            isEditorTextCompositionActive: false,
            source.TextLength,
            out _);

        Assert.IsFalse(navigated);
        Assert.AreEqual(selectionStart, MixedSource.IndexOf(
            "\u0633\u0644\u0627\u0645",
            StringComparison.Ordinal));
        Assert.AreEqual(selectionLength, "\u0633\u0644\u0627\u0645".Length);
        Assert.AreEqual(MixedSource, source.Text);
        Assert.IsFalse(source.UndoStack.CanUndo);
        Assert.AreEqual("label", inspector.SelectedElement.Element.Id);
        Assert.IsTrue(inspector.Properties.Any(property =>
            property.Name == "id" && property.Value == "label"));
    }

    [TestMethod]
    public void RestoredEmptySelectionDoesNotInventARootPrimary()
    {
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(MixedSource).Document!;
        DocumentInspectorViewModel inspector = new();

        inspector.Load(
            index,
            preferredSelection: null,
            source: MixedSource,
            selectFirstRootWhenSelectionIsEmpty: false);

        Assert.IsFalse(inspector.HasSelection);
        Assert.IsNull(inspector.SelectedElement);
        Assert.IsNull(inspector.SelectedLayer);
        Assert.AreEqual(0, inspector.Properties.Count);
    }

    [TestMethod]
    public void PropertyCommitPreservesMixedTextAndCreatesOneDocumentUndoUnit()
    {
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(MixedSource).Document!;
        SvgElementNode rectangle = index.Elements.Single(element =>
            element.Id == "shape");
        SvgAttributeEditResult edit = new SvgAttributeEditService().CreateEdit(
            MixedSource,
            rectangle,
            "fill",
            "blue");
        TextDocument document = new(MixedSource);
        document.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(document, edit.Edit!);

        const string expected =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\">\u0633\u0644\u0627\u0645 English</text><rect id=\"shape\" fill=\"blue\"/></svg>";
        Assert.AreEqual(expected, document.Text);
        Assert.IsTrue(document.UndoStack.CanUndo);
        document.UndoStack.Undo();
        Assert.AreEqual(MixedSource, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        document.UndoStack.Redo();
        Assert.AreEqual(expected, document.Text);
    }

    [TestMethod]
    public void AvalonEditSelectionCallbackOrderingIsBoundFromObservedDocumentState()
    {
        const string beforeSource = "before";
        const string afterSource = "after";
        SvgMultiSelectionState selection = SvgMultiSelectionState.Empty(0);
        List<string> events = [];
        TextDocument document = new(beforeSource);
        document.TextChanged += (_, _) =>
            events.Add($"text:{document.Text}");
        SvgSelectionUndoOperation selectionUndo = new(
            SvgSelectionRestoreTarget.Create(selection, beforeSource),
            SvgSelectionRestoreTarget.Create(selection, afterSource),
            target => events.Add(
                $"restore:{target.Matches(document.Text)}:{document.Text}"));

        new AvalonEditDocumentEditService().Apply(
            document,
            new SourceTextEdit(0, beforeSource.Length, afterSource),
            selectionUndo);
        events.Clear();

        document.UndoStack.Undo();

        Assert.AreEqual(
            2,
            events.Count,
            $"Undo events: {string.Join(" | ", events)}");
        Assert.AreEqual(
            "restore:False:after",
            events[0],
            $"Undo events: {string.Join(" | ", events)}");
        Assert.AreEqual(
            "text:before",
            events[1],
            $"Undo events: {string.Join(" | ", events)}");
        events.Clear();

        document.UndoStack.Redo();

        Assert.AreEqual(
            2,
            events.Count,
            $"Redo events: {string.Join(" | ", events)}");
        Assert.AreEqual(
            "restore:True:after",
            events[0],
            $"Redo events: {string.Join(" | ", events)}");
        Assert.AreEqual(
            "text:after",
            events[1],
            $"Redo events: {string.Join(" | ", events)}");
    }

    [TestMethod]
    public void InvalidThenValidAcceptsOnlyTheNewestRevisionAndExactSource()
    {
        const string invalid =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>\u0633\u0644\u0627\u0645</svg>";
        SourceRevisionTracker tracker = new();
        long invalidRevision = tracker.Advance(SourceChangeOrigin.SourceEditor);
        SvgDocumentIndexResult invalidResult =
            new SvgDocumentIndexService().Build(invalid);
        long validRevision = tracker.Advance(SourceChangeOrigin.SourceEditor);
        SvgDocumentIndexResult validResult =
            new SvgDocumentIndexService().Build(MixedSource);

        Assert.IsFalse(invalidResult.Validation.IsValid);
        Assert.IsNull(invalidResult.Document);
        Assert.IsFalse(tracker.IsCurrent(invalidRevision));
        Assert.IsTrue(tracker.IsCurrent(validRevision));
        Assert.IsTrue(validResult.Validation.IsValid);
        Assert.IsNotNull(validResult.Document);
        Assert.AreEqual("\u0633\u0644\u0627\u0645 English", MixedSource[
            (MixedSource.IndexOf('>', MixedSource.IndexOf("<text", StringComparison.Ordinal)) + 1)..
            MixedSource.IndexOf("</text>", StringComparison.Ordinal)]);
    }
}
