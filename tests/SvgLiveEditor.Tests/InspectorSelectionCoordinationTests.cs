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
    [DataRow("Persian", "فارسی")]
    [DataRow("English", "English")]
    [DataRow("Mixed bidi", "هولی shit این بده")]
    [DataRow("Bidi digits punctuation", "نسخه 10.0: SVG, خوبه!")]
    [DataRow("Multiline", "خط اول\nEnglish line\nخط سوم")]
    [DataRow("Leading and trailing whitespace", "  فارسی English  ")]
    [DataRow("Lexical XML entities", "فارسی &amp; English &lt;test&gt;")]
    [DataRow("Supplementary Unicode", "فارسی 😀 English")]
    public void PreviewNavigationSelectsExactRawDirectText(
        string caseName,
        string expected)
    {
        _ = caseName;
        AssertExactPreviewTextSelection(expected);
    }

    [TestMethod]
    public void PreviewNavigationPlacesCaretInsideEmptyDirectText()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"empty\" x=\"70\" y=\"93\"></text></svg>";
        CoordinationHarness harness = new(source);
        SvgElementNode text = harness.FindElement("empty").Element;

        harness.SelectFromPreview(text);

        Assert.AreEqual(text.StartTagSpan.End, harness.SelectionStart);
        Assert.AreEqual(0, harness.SelectionLength);
        Assert.AreEqual(string.Empty, harness.SelectedText);
        Assert.AreEqual(source, harness.Document.Text);
        Assert.IsFalse(harness.Document.UndoStack.CanUndo);
        Assert.AreEqual("empty", harness.Inspector.SelectedElement!.Element.Id);
        Assert.IsTrue(harness.Inspector.Properties.Any(property =>
            property.Name == "id" && property.Value == "empty"));
    }

    [TestMethod]
    public void PreviewNavigationSelectsWhitespaceOnlyDirectTextExactly()
    {
        AssertExactPreviewTextSelection(" \t  ");
    }

    [TestMethod]
    public void TypingAfterPreviewTextSelectionReplacesOnlyDirectContent()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"edit\" x=\"70\" y=\"93\" direction=\"rtl\">هولی shit این بده</text></svg>";
        const string replacement = "متن تازه";
        CoordinationHarness harness = new(source);
        harness.SelectFromPreview(harness.FindElement("edit").Element);
        Assert.IsFalse(harness.Document.UndoStack.CanUndo);

        harness.Type(replacement);

        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"edit\" x=\"70\" y=\"93\" direction=\"rtl\">متن تازه</text></svg>",
            harness.Document.Text);
        Assert.IsTrue(harness.Document.UndoStack.CanUndo);
        harness.Undo();
        Assert.AreEqual(source, harness.Document.Text);
    }

    [TestMethod]
    public void PreviewNavigationFallsBackForComplexAndSelfClosingText()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path id=\"curve\" d=\"M0 0 L10 0\"/><text id=\"nested\"><tspan>سلام</tspan></text><text id=\"path\"><textPath href=\"#curve\">hello</textPath></text><text id=\"cdata\"><![CDATA[hello]]></text><text id=\"self\"/></svg>";
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgSourceNavigationSpanService service = new();

        foreach (SvgElementNode text in index.Elements.Where(element =>
                     element.Name == "text"))
        {
            Assert.AreEqual(
                text.StartTagSpan,
                service.GetPreferredSpan(
                    source,
                    text,
                    InspectorSelectionOrigin.PreviewNavigation));
        }
    }

    [TestMethod]
    public void PreviewSelectionSurvivesProgrammaticRefreshButImeAndStaleNavigationFailClosed()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"value\">فارسی &amp; English</text></svg>";
        CoordinationHarness harness = new(source);
        SvgElementViewModel text = harness.FindElement("value");
        harness.SelectFromPreview(text.Element);
        (int Start, int Length, string Text) expected =
            (harness.SelectionStart, harness.SelectionLength, harness.SelectedText);

        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(source).Document!,
            text.Element.Identity);
        harness.RaiseTreeSelectionChanged();
        AssertSelection(harness, expected.Start, expected.Text);

        harness.SetSelection(3, 2);
        harness.IsTextCompositionActive = true;
        harness.Navigate(
            harness.Inspector.SelectedElement!,
            InspectorSelectionOrigin.PreviewNavigation);
        Assert.AreEqual(3, harness.SelectionStart);
        Assert.AreEqual(2, harness.SelectionLength);

        harness.IsTextCompositionActive = false;
        harness.Navigate(
            harness.Inspector.SelectedElement!,
            InspectorSelectionOrigin.PreviewNavigation,
            indexRevision: harness.SourceRevision - 1);
        Assert.AreEqual(3, harness.SelectionStart);
        Assert.AreEqual(2, harness.SelectionLength);
        Assert.AreEqual(source, harness.Document.Text);
    }

    [TestMethod]
    public void PreviewNavigationWithDuplicateAuthoredIds_SelectsExactStructuralSpan()
    {
        const string duplicateIds =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\" x=\"1\"/><rect id=\"same\" x=\"2\"/></svg>";
        CoordinationHarness harness = new(duplicateIds);
        SvgElementNode second = harness.Inspector.DocumentIndex!.Elements
            .Where(element => element.Id == "same")
            .OrderBy(element => element.StartTagSpan.Start)
            .Last();

        harness.Inspector.SelectNode(
            second,
            InspectorSelectionOrigin.PreviewNavigation);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(second.StartTagSpan.Start, harness.SelectionStart);
        Assert.AreEqual(second.StartTagSpan.Length, harness.SelectionLength);
        StringAssert.Contains(
            harness.Document.GetText(
                harness.SelectionStart,
                harness.SelectionLength),
            "x=\"2\"");
    }

    [TestMethod]
    public void DuplicateValidTextIdsRemainStructurallyExactAcrossRefreshUndoAndRedo()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"same-id\">اول</text><text id=\"same-id\">دوم</text></svg>";
        CoordinationHarness harness = new(source);
        SvgElementNode[] duplicates = harness.Inspector.DocumentIndex!.Elements
            .Where(element => element.Id == "same-id")
            .OrderBy(element => element.StructuralPath, StringComparer.Ordinal)
            .ToArray();

        harness.SelectFromPreview(duplicates[0]);
        AssertSelection(harness, duplicates[0].StartTagSpan.End, "اول");
        harness.SelectFromPreview(duplicates[1]);
        AssertSelection(harness, duplicates[1].StartTagSpan.End, "دوم");
        SvgElementIdentity intended = duplicates[1].Identity;

        harness.ApplyIndex(
            new SvgDocumentIndexService().Build(source).Document!,
            intended);
        harness.RaiseTreeSelectionChanged();
        AssertSelection(harness, duplicates[1].StartTagSpan.End, "دوم");
        Assert.AreEqual("0/1", harness.Inspector.SelectedElement!.Element.StructuralPath);

        int rootTagEnd = harness.Document.Text.IndexOf('>');
        harness.Replace(rootTagEnd, 0, " data-name=\"root\"");
        RebuildAndSelectIntendedDuplicate(harness, intended);
        Assert.AreEqual("دوم", harness.SelectedText);
        Assert.AreEqual(2, CountOccurrences(harness.Document.Text, "id=\"same-id\""));

        harness.Undo();
        RebuildAndSelectIntendedDuplicate(harness, intended);
        Assert.AreEqual("دوم", harness.SelectedText);
        harness.Redo();
        RebuildAndSelectIntendedDuplicate(harness, intended);
        Assert.AreEqual("دوم", harness.SelectedText);
        Assert.AreEqual(2, CountOccurrences(harness.Document.Text, "id=\"same-id\""));
    }

    [TestMethod]
    public void PreviewMultiSelectionPrimaryChange_NavigatesOnlyNewPrimary()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/></svg>";
        CoordinationHarness harness = new(source);
        SvgElementNode first = harness.Inspector.DocumentIndex!.Elements
            .Single(element => element.Id == "a");
        SvgElementNode second = harness.Inspector.DocumentIndex!.Elements
            .Single(element => element.Id == "b");
        SvgMultiSelectionService selections = new();
        SvgMultiSelectionState state = selections.Replace(
            harness.SourceRevision,
            first.Identity);
        SvgMultiSelectionChange changed = selections.Toggle(
            state,
            harness.SourceRevision,
            second.Identity);

        Assert.IsTrue(changed.IsSuccess);
        Assert.AreEqual(2, changed.State.Identities.Count);
        Assert.AreEqual(second.Identity, changed.State.Primary);
        Assert.IsNotNull(changed.State.Primary);
        harness.Inspector.SelectNode(
            harness.Inspector.DocumentIndex.FindBestMatch(
                changed.State.Primary!),
            InspectorSelectionOrigin.PreviewNavigation);
        harness.RaiseTreeSelectionChanged();

        Assert.AreEqual(second.StartTagSpan.Start, harness.SelectionStart);
        StringAssert.StartsWith(
            harness.Document.GetText(
                harness.SelectionStart,
                harness.SelectionLength),
            "<circle");
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

    private static void AssertExactPreviewTextSelection(string expected)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"70\" y=\"93\" direction=\"rtl\">{expected}</text></svg>";
        CoordinationHarness harness = new(source);
        SvgElementNode text = harness.Inspector.DocumentIndex!.Elements
            .Single(element => element.Name == "text");

        harness.SelectFromPreview(text);

        AssertSelection(harness, text.StartTagSpan.End, expected);
        Assert.AreEqual(source, harness.Document.Text);
        Assert.IsFalse(harness.Document.UndoStack.CanUndo);
        Assert.AreEqual("text", harness.Inspector.SelectedElement!.Element.Name);
    }

    private static void AssertSelection(
        CoordinationHarness harness,
        int expectedStart,
        string expectedText)
    {
        Assert.AreEqual(expectedStart, harness.SelectionStart);
        Assert.AreEqual(expectedText.Length, harness.SelectionLength);
        Assert.AreEqual(expectedText, harness.SelectedText);
        Assert.AreEqual(
            new SourceSpan(expectedStart, expectedText.Length),
            harness.SelectionSpan);
    }

    private static void RebuildAndSelectIntendedDuplicate(
        CoordinationHarness harness,
        SvgElementIdentity intended)
    {
        SvgDocumentIndex rebuilt = new SvgDocumentIndexService()
            .Build(harness.Document.Text).Document!;
        harness.ApplyIndex(rebuilt, intended);
        SvgElementNode selected = harness.Inspector.SelectedElement!.Element;
        Assert.AreEqual(intended.StructuralPath, selected.StructuralPath);
        harness.Navigate(
            harness.Inspector.SelectedElement,
            InspectorSelectionOrigin.PreviewNavigation);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(
                   value,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private sealed class CoordinationHarness
    {
        private readonly InspectorSelectionCoordinator _coordinator = new();
        private readonly SvgSourceNavigationSpanService
            _sourceNavigationSpanService = new();
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

        public string SelectedText => Document.GetText(
            SelectionStart,
            SelectionLength);

        public SourceSpan SelectionSpan => new(
            SelectionStart,
            SelectionLength);

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

        public void Replace(int offset, int length, string text)
        {
            Document.Replace(offset, length, text);
            _revisions.Advance();
        }

        public void Undo()
        {
            Document.UndoStack.Undo();
            _revisions.Advance();
        }

        public void Redo()
        {
            Document.UndoStack.Redo();
            _revisions.Advance();
        }

        public void SelectFromPreview(SvgElementNode element)
        {
            Inspector.SelectNode(
                element,
                InspectorSelectionOrigin.PreviewNavigation);
            RaiseTreeSelectionChanged();
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
                InspectorSelectionOrigin.InspectorRestore,
                source: Document.Text);
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
            SourceSpan preferredSpan = _sourceNavigationSpanService
                .GetPreferredSpan(Document.Text, element.Element, origin);
            if (_coordinator.TryGetNavigationSpan(
                    origin,
                    preferredSpan,
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
