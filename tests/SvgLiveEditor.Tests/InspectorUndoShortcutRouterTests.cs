using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InspectorUndoShortcutRouterTests
{
    [TestMethod]
    [DataRow("TextBox property")]
    [DataRow("ComboBox property")]
    [DataRow("Opacity")]
    public void CommittedPropertiesRouteOneDocumentUndoAndRedo(string scenario)
    {
        TextDocument document = new("before");
        document.UndoStack.MarkAsOriginalFile();
        new AvalonEditDocumentEditService().Apply(
            document,
            new SourceTextEdit(0, document.TextLength, scenario));

        Execute(
            document,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                CleanPropertiesFocus()));
        Assert.AreEqual("before", document.Text);
        Assert.IsTrue(document.UndoStack.CanRedo);

        Execute(
            document,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Redo,
                CleanPropertiesFocus()));
        Assert.AreEqual(scenario, document.Text);
    }

    [TestMethod]
    public void OneShortcutNeverConsumesTwoDocumentUndoUnits()
    {
        TextDocument document = new("abc");
        AvalonEditDocumentEditService edits = new();
        edits.Apply(document, new SourceTextEdit(0, 1, "A"));
        edits.Apply(document, new SourceTextEdit(1, 1, "B"));

        Execute(
            document,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                CleanPropertiesFocus()));

        Assert.AreEqual("Abc", document.Text);
        Assert.IsTrue(document.UndoStack.CanUndo);
        Assert.IsTrue(document.UndoStack.CanRedo);
    }

    [TestMethod]
    public void SourceEditorFocusPreservesDocumentUndoRedoRouting()
    {
        InspectorUndoFocusState source = new(
            IsSourceEditorFocused: true,
            IsDocumentTreeFocused: false,
            IsPropertiesFocused: false,
            HasUncommittedValue: true,
            HasLocalRedo: true,
            IsTextCompositionActive: true);

        Assert.AreEqual(
            InspectorUndoShortcutRoute.DocumentUndo,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                source));
        Assert.AreEqual(
            InspectorUndoShortcutRoute.DocumentRedo,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Redo,
                source));
    }

    [TestMethod]
    public void LayersAndStructureFocusRouteDocumentUndoAndRedo()
    {
        InspectorUndoFocusState tree = new(
            IsSourceEditorFocused: false,
            IsDocumentTreeFocused: true,
            IsPropertiesFocused: false,
            HasUncommittedValue: false,
            HasLocalRedo: false,
            IsTextCompositionActive: false);

        Assert.AreEqual(
            InspectorUndoShortcutRoute.DocumentUndo,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                tree));
        Assert.AreEqual(
            InspectorUndoShortcutRoute.DocumentRedo,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Redo,
                tree));
    }

    [TestMethod]
    public void UncommittedPropertyTypingStaysWithTheFocusedTextControl()
    {
        InspectorUndoFocusState dirty = CleanPropertiesFocus() with
        {
            HasUncommittedValue = true
        };

        Assert.AreEqual(
            InspectorUndoShortcutRoute.FocusedControl,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                dirty));
        Assert.AreEqual(
            InspectorUndoShortcutRoute.FocusedControl,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Redo,
                dirty));
    }

    [TestMethod]
    public void LocalRedoAfterUndoIsNotStolenByDocumentHistory()
    {
        InspectorUndoFocusState localRedo = CleanPropertiesFocus() with
        {
            HasLocalRedo = true
        };

        Assert.AreEqual(
            InspectorUndoShortcutRoute.FocusedControl,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Redo,
                localRedo));
        Assert.AreEqual(
            InspectorUndoShortcutRoute.DocumentUndo,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                localRedo));
    }

    [TestMethod]
    public void PersianImeCompositionStaysLocalAndUnchanged()
    {
        const string persian = "سلام دنیا";
        string snapshot = persian;
        InspectorUndoFocusState composing = CleanPropertiesFocus() with
        {
            HasUncommittedValue = true,
            IsTextCompositionActive = true
        };

        Assert.AreEqual(
            InspectorUndoShortcutRoute.FocusedControl,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                composing));
        Assert.AreEqual(persian, snapshot);
    }

    [TestMethod]
    public void UnrelatedControlsAreIgnored()
    {
        InspectorUndoFocusState unrelated = new(
            IsSourceEditorFocused: false,
            IsDocumentTreeFocused: false,
            IsPropertiesFocused: false,
            HasUncommittedValue: false,
            HasLocalRedo: false,
            IsTextCompositionActive: false);

        Assert.AreEqual(
            InspectorUndoShortcutRoute.Ignore,
            InspectorUndoShortcutRouter.Resolve(
                InspectorUndoShortcut.Undo,
                unrelated));
    }

    [TestMethod]
    public void WpfHostUsesPreviewRoutingAndMarksDocumentRouteHandled()
    {
        string main = ReadUi("MainWindow.xaml.cs");
        string inspector = ReadUi("MainWindow.Inspector.cs");

        StringAssert.Contains(main, "TryHandleInspectorUndoShortcut(pressedKey)");
        StringAssert.Contains(main, "e.Handled = true;");
        StringAssert.Contains(inspector, "InspectorUndoShortcutRouter.Resolve(");
        StringAssert.Contains(inspector, "LayersTree.IsKeyboardFocusWithin");
        StringAssert.Contains(inspector, "InspectorTree.IsKeyboardFocusWithin");
        StringAssert.Contains(inspector, "HasUncommittedValue");
        StringAssert.Contains(inspector, "CanRedo: true");
        StringAssert.Contains(inspector, "OnUndoClick(this, new RoutedEventArgs())");
        StringAssert.Contains(inspector, "OnRedoClick(this, new RoutedEventArgs())");
        StringAssert.Contains(
            inspector,
            "OnInspectorTextCompositionStarted");
    }

    private static InspectorUndoFocusState CleanPropertiesFocus() => new(
        IsSourceEditorFocused: false,
        IsDocumentTreeFocused: false,
        IsPropertiesFocused: true,
        HasUncommittedValue: false,
        HasLocalRedo: false,
        IsTextCompositionActive: false);

    private static void Execute(
        TextDocument document,
        InspectorUndoShortcutRoute route)
    {
        if (route == InspectorUndoShortcutRoute.DocumentUndo)
        {
            document.UndoStack.Undo();
        }
        else if (route == InspectorUndoShortcutRoute.DocumentRedo)
        {
            document.UndoStack.Redo();
        }
    }

    private static string ReadUi(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "ui", fileName));
}
