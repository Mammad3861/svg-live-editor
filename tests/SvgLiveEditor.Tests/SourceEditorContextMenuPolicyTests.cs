using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SourceEditorContextMenuPolicyTests
{
    [TestMethod]
    public void DefinitionHasStandardCommandOrderAndSeparators()
    {
        string[] actual = SourceEditorContextMenuPolicy.Items
            .Select(item => item.IsSeparator
                ? "-"
                : item.Command!.Value.ToString())
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Undo", "Redo", "-", "Cut", "Copy", "Paste",
                "Delete", "-", "SelectAll"
            },
            actual);
    }

    [TestMethod]
    public void EnablementUsesHistorySelectionClipboardAndCompositionState()
    {
        SourceEditorCommandState enabled = new(
            CanUndo: true,
            CanRedo: true,
            HasSelection: true,
            HasText: true,
            CanPasteText: true,
            IsReadOnly: false,
            IsCompositionActive: false);

        foreach (SourceEditorContextCommand command in
                 Enum.GetValues<SourceEditorContextCommand>())
        {
            Assert.IsTrue(SourceEditorContextMenuPolicy.IsEnabled(
                command,
                enabled),
                command.ToString());
        }

        SourceEditorCommandState empty = new(
            CanUndo: false,
            CanRedo: false,
            HasSelection: false,
            HasText: false,
            CanPasteText: false,
            IsReadOnly: false,
            IsCompositionActive: false);
        foreach (SourceEditorContextCommand command in
                 Enum.GetValues<SourceEditorContextCommand>())
        {
            Assert.IsFalse(SourceEditorContextMenuPolicy.IsEnabled(
                command,
                empty),
                command.ToString());
        }

        SourceEditorCommandState composing = enabled with
        {
            IsCompositionActive = true
        };
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsEnabled(
            SourceEditorContextCommand.Cut,
            composing));
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsEnabled(
            SourceEditorContextCommand.Paste,
            composing));
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsEnabled(
            SourceEditorContextCommand.Undo,
            composing));
        Assert.IsTrue(SourceEditorContextMenuPolicy.IsEnabled(
            SourceEditorContextCommand.Copy,
            composing));
    }

    [TestMethod]
    public void RightClickInsideSelectionPreservesItAndOutsideDoesNot()
    {
        Assert.IsTrue(SourceEditorContextMenuPolicy.IsOffsetInsideSelection(
            offset: 7,
            selectionStart: 5,
            selectionLength: 4));
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsOffsetInsideSelection(
            offset: 4,
            selectionStart: 5,
            selectionLength: 4));
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsOffsetInsideSelection(
            offset: 9,
            selectionStart: 5,
            selectionLength: 4));
        Assert.IsFalse(SourceEditorContextMenuPolicy.IsOffsetInsideSelection(
            offset: 5,
            selectionStart: 5,
            selectionLength: 0));
    }

    [TestMethod]
    public void PersianCutPasteUndoAndRedoPreserveExactText()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام دنیا</text></svg>";
        TextDocument document = new(source);
        int start = source.IndexOf("سلام", StringComparison.Ordinal);
        string clipboardText = document.GetText(start, "سلام".Length);

        document.Remove(start, clipboardText.Length);
        Assert.IsFalse(document.Text.Contains("سلام", StringComparison.Ordinal));
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        Assert.IsFalse(document.Text.Contains("سلام", StringComparison.Ordinal));

        document.Insert(start, clipboardText);
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Undo();
        Assert.IsFalse(document.Text.Contains("سلام", StringComparison.Ordinal));
        document.UndoStack.Redo();
        Assert.AreEqual(source, document.Text);
    }

    [TestMethod]
    public void MainWindowWiresNativeAvalonEditMethodsWithoutCustomClipboardFormats()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "MainWindow.SourceEditor.cs"));

        StringAssert.Contains(source, "SourceEditor.Undo()");
        StringAssert.Contains(source, "SourceEditor.Redo()");
        StringAssert.Contains(source, "SourceEditor.Cut()");
        StringAssert.Contains(source, "SourceEditor.Copy()");
        StringAssert.Contains(source, "SourceEditor.Paste()");
        StringAssert.Contains(source, "SourceEditor.Delete()");
        StringAssert.Contains(source, "SourceEditor.SelectAll()");
        StringAssert.Contains(source, "TextDataFormat.UnicodeText");
        Assert.IsFalse(source.Contains("PreviewWebView", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("DataFormats.Html", StringComparison.Ordinal));
    }
}
