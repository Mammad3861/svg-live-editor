using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InspectorSelectionCoordinationTests
{
    private const string Source = """
        <svg xmlns="http://www.w3.org/2000/svg">
          <g id="content">
            <text id="greeting" x="12" y="24">سلام</text>
            <rect id="box" x="4" y="40" width="80" height="20"/>
          </g>
        </svg>
        """;

    [TestMethod]
    public void TypingFollowedByDelayedIndexing_DoesNotChangeEditorSelection()
    {
        CoordinationHarness harness = new(Source);
        SvgElementIdentity greeting = harness.SelectInspectorForCaret("greeting");
        int insertionOffset = harness.Document.Text.IndexOf(
            "</text>",
            StringComparison.Ordinal);
        harness.SetCaret(insertionOffset);

        harness.Type(" فارسی");
        int expectedCaret = harness.SelectionStart;
        SvgDocumentIndex delayedIndex =
            new SvgDocumentIndexService().Build(harness.Document.Text).Document!;
        harness.ApplyIndex(delayedIndex, greeting);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(expectedCaret, harness.SelectionStart);
        Assert.AreEqual(0, harness.SelectionLength);
        StringAssert.Contains(harness.Document.Text, "سلام فارسی</text>");
    }

    [TestMethod]
    public void ProgrammaticRestoreAndCaretSync_DoNotChangeEditorSelection()
    {
        CoordinationHarness harness = new(Source);
        harness.SetSelection(8, 3);
        int selectionStart = harness.SelectionStart;
        int selectionLength = harness.SelectionLength;

        SvgElementIdentity greeting = harness.SelectInspectorForCaret("greeting");
        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(Source).Document!,
            greeting);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(selectionStart, harness.SelectionStart);
        Assert.AreEqual(selectionLength, harness.SelectionLength);
    }

    [TestMethod]
    public void ImeCompletionThenIndexing_DoesNotSelectTheStartTag()
    {
        CoordinationHarness harness = new(Source);
        SvgElementIdentity greeting = harness.SelectInspectorForCaret("greeting");
        int insertionOffset = Source.IndexOf("</text>", StringComparison.Ordinal);
        harness.SetCaret(insertionOffset);
        harness.IsTextCompositionActive = true;
        harness.Type(" دنیا");
        harness.IsTextCompositionActive = false;

        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(harness.Document.Text).Document!,
            greeting);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(0, harness.SelectionLength);
        StringAssert.Contains(harness.Document.Text, "سلام دنیا</text>");
    }

    [TestMethod]
    public void StaleSourceSpan_CannotBeSelected()
    {
        CoordinationHarness harness = new(Source);
        SvgElementViewModel staleElement =
            harness.FindElement("greeting");
        harness.SetCaret(Source.IndexOf("سلام", StringComparison.Ordinal));
        harness.Type("متن ");
        int expectedCaret = harness.SelectionStart;

        harness.Navigate(
            staleElement,
            InspectorSelectionOrigin.ExplicitTreeNavigation,
            indexRevision: harness.SourceRevision - 1);

        Assert.AreEqual(expectedCaret, harness.SelectionStart);
        Assert.AreEqual(0, harness.SelectionLength);
    }

    [TestMethod]
    public void ExplicitTreeNavigation_SelectsOnlyTheCurrentStartTag()
    {
        CoordinationHarness harness = new(Source);
        SvgElementViewModel greeting = harness.FindElement("greeting");

        harness.Navigate(
            greeting,
            InspectorSelectionOrigin.ExplicitTreeNavigation);

        SourceSpan expected = greeting.Element.StartTagSpan;
        Assert.AreEqual(expected.Start, harness.SelectionStart);
        Assert.AreEqual(expected.Length, harness.SelectionLength);
        StringAssert.StartsWith(
            harness.Document.GetText(
                harness.SelectionStart,
                harness.SelectionLength),
            "<text");
    }

    [TestMethod]
    public void ContinuedPersianTypingAfterInspectorRestore_InsertsAtCaret()
    {
        CoordinationHarness harness = new(Source);
        SvgElementIdentity greeting = harness.SelectInspectorForCaret("greeting");
        int insertionOffset = Source.IndexOf("</text>", StringComparison.Ordinal);
        harness.SetCaret(insertionOffset);
        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(Source).Document!,
            greeting);
        harness.RaiseTreeSelectionChanged();

        harness.Type(" فارسی");

        StringAssert.Contains(
            harness.Document.Text,
            "<text id=\"greeting\" x=\"12\" y=\"24\">سلام فارسی</text>");
        Assert.AreEqual(0, harness.SelectionLength);
    }

    [TestMethod]
    public void RapidTypingThenLatestDebounceCompletion_PreservesCaretAndText()
    {
        CoordinationHarness harness = new(Source);
        SvgElementIdentity greeting = harness.SelectInspectorForCaret("greeting");
        harness.SetCaret(Source.IndexOf("</text>", StringComparison.Ordinal));
        string[] fragments = [" م", "ت", "ن", " ", "ف", "ا", "ر", "س", "ی"];
        foreach (string fragment in fragments)
        {
            harness.Type(fragment);
        }

        int expectedCaret = harness.SelectionStart;
        string expectedSource = harness.Document.Text;
        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(expectedSource).Document!,
            greeting);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(expectedSource, harness.Document.Text);
        Assert.AreEqual(expectedCaret, harness.SelectionStart);
        Assert.AreEqual(0, harness.SelectionLength);
    }

    [TestMethod]
    public void PropertyEditWithStaleRevision_IsRejectedWithoutChangingSource()
    {
        CoordinationHarness harness = new(Source);
        SvgElementViewModel rectangle = harness.FindElement("box");
        long indexedRevision = harness.SourceRevision;
        string indexedSource = harness.Document.Text;
        harness.SetCaret(Source.IndexOf("</text>", StringComparison.Ordinal));
        harness.Type(" تازه");

        InspectorSourceGuard guard = new();
        bool canApply = guard.CanUseIndex(
            isIndexCurrent: true,
            indexRevision: indexedRevision,
            sourceRevision: harness.SourceRevision,
            isEditorTextCompositionActive: false);

        Assert.IsFalse(canApply);
        Assert.AreNotEqual(indexedSource, harness.Document.Text);
        Assert.AreEqual(
            "20",
            rectangle.Element.FindAttribute("height")!.RawValue);
        StringAssert.Contains(harness.Document.Text, "سلام تازه</text>");
    }

    private sealed class CoordinationHarness
    {
        private readonly InspectorSelectionCoordinator _coordinator = new();
        private readonly SourceRevisionTracker _revisions = new();
        private long _indexRevision;

        public CoordinationHarness(string source)
        {
            Document = new TextDocument(source);
            Inspector = new DocumentInspectorViewModel();
            _revisions.Advance();
            ApplyIndex(
                new SvgDocumentIndexService().Build(source).Document!,
                preferredSelection: null);
            RaiseTreeSelectionChanged();
        }

        public TextDocument Document { get; }

        public DocumentInspectorViewModel Inspector { get; }

        public bool IsTextCompositionActive { get; set; }

        public int SelectionStart { get; private set; }

        public int SelectionLength { get; private set; }

        public long SourceRevision => _revisions.Current;

        public void SetCaret(int offset) => SetSelection(offset, 0);

        public void SetSelection(int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
        }

        public void Type(string text)
        {
            Document.Replace(SelectionStart, SelectionLength, text);
            SelectionStart += text.Length;
            SelectionLength = 0;
            _revisions.Advance();
        }

        public SvgElementIdentity SelectInspectorForCaret(string id)
        {
            SvgElementViewModel element = FindElement(id);
            Inspector.SelectNode(
                element.Element,
                InspectorSelectionOrigin.SourceCaretSync);
            RaiseTreeSelectionChanged();
            return element.Element.Identity;
        }

        public SvgElementViewModel FindElement(string id)
        {
            SvgElementNode node = Inspector.DocumentIndex!.Elements
                .Single(element => element.Id == id);
            return Inspector.FindViewModel(node)!;
        }

        public void ApplyIndex(
            SvgDocumentIndex index,
            SvgElementIdentity? preferredSelection)
        {
            _indexRevision = _revisions.Current;
            Inspector.Load(
                index,
                preferredSelection,
                InspectorSelectionOrigin.InspectorRestore);
        }

        public void RaiseTreeSelectionChanged()
        {
            SvgElementViewModel element = Inspector.SelectedElement!;
            InspectorSelectionOrigin origin =
                element.ConsumePendingSelectionOrigin()
                ?? InspectorSelectionOrigin.InspectorRestore;
            Inspector.AcceptTreeSelection(element);
            Navigate(element, origin);
        }

        public void Navigate(
            SvgElementViewModel element,
            InspectorSelectionOrigin origin,
            long? indexRevision = null)
        {
            if (_coordinator.TryGetNavigationSpan(
                    origin,
                    element.Element.StartTagSpan,
                    isIndexCurrent: true,
                    indexRevision ?? _indexRevision,
                    _revisions.Current,
                    IsTextCompositionActive,
                    Document.TextLength,
                    out SourceSpan span))
            {
                SetSelection(span.Start, span.Length);
            }
        }
    }
}
