using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgDirectTextContentEditServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgDirectTextContentEditService _service = new();

    [TestMethod]
    [DataRow("English text")]
    [DataRow("متن فارسی ۱۲۳")]
    [DataRow("English و فارسی — (test)!؟")]
    [DataRow("  leading  and trailing  ")]
    [DataRow("")]
    public void EligibleDirectTextReplacesOnlyTheExactContentSpan(
        string replacement)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <!-- keep فارسی -->\r\n  <text id=\"target\" x=\"10\" y=\"20\" direction=\"rtl\">Old &amp; exact</text>\r\n  <rect id=\"keep\" width=\"7\" height=\"8\"/>\r\n</svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode text = Find(document, "target");

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            text,
            replacement);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        string updated = result.Edit.Apply(source);
        StringAssert.StartsWith(
            updated,
            source[..text.StartTagSpan.End]);
        StringAssert.EndsWith(
            updated,
            source[(text.StartTagSpan.End
                + "Old &amp; exact".Length)..]);
        Assert.AreEqual(replacement, ReadText(updated, "target"));
        StringAssert.Contains(updated, "<!-- keep فارسی -->");
        StringAssert.Contains(updated, "<rect id=\"keep\" width=\"7\" height=\"8\"/>");
        StringAssert.Contains(updated, "\r\n");
    }

    [TestMethod]
    public void XmlSensitiveUnicodeIsEscapedWithoutReserializingSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" data-note=\"untouched &amp; exact\">old</text></svg>";
        const string replacement = "سلام & <SVG> ]]> 😀";
        SvgDocumentIndex document = Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "target"),
            replacement);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string updated = result.Edit!.Apply(source);
        StringAssert.Contains(
            updated,
            "data-note=\"untouched &amp; exact\">سلام &amp; &lt;SVG&gt; ]]&gt; 😀</text>");
        Assert.AreEqual(replacement, ReadText(updated, "target"));
    }

    [TestMethod]
    public void EqualDecodedValuePreservesTheOriginalEntitySpelling()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\">A &amp; B</text></svg>";
        SvgDocumentIndex document = Build(source);

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "target"),
            "A & B");

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(source, "A &amp; B");
    }

    [TestMethod]
    public void ExplicitEmptyTextCanBeFilledButSelfClosingTextFailsClosed()
    {
        const string explicitEmpty =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\"></text></svg>";
        SvgDocumentIndex explicitDocument = Build(explicitEmpty);
        SvgAttributeEditResult filled = _service.CreateEdit(
            explicitEmpty,
            explicitDocument,
            Find(explicitDocument, "target"),
            "متن");
        Assert.IsTrue(filled.IsSuccess, filled.ErrorMessage);
        Assert.AreEqual("متن", ReadText(filled.Edit!.Apply(explicitEmpty), "target"));

        const string selfClosing =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\"/></svg>";
        SvgDocumentIndex selfClosingDocument = Build(selfClosing);
        SvgAttributeEditResult rejected = _service.CreateEdit(
            selfClosing,
            selfClosingDocument,
            Find(selfClosingDocument, "target"),
            "text");
        Assert.IsFalse(rejected.IsSuccess);
        StringAssert.Contains(rejected.ErrorMessage!, "span");
    }

    [TestMethod]
    [DataRow("<text id=\"target\"><tspan>nested</tspan></text>", "tspan")]
    [DataRow("<text id=\"target\"><textPath href=\"#p\">path</textPath></text>", "textPath")]
    [DataRow("<text id=\"target\">before<!-- comment -->after</text>", "plain text-content")]
    [DataRow("<text id=\"target\"><![CDATA[cdata]]></text>", "CDATA")]
    [DataRow("<text id=\"target\" transform=\"translate(1 2)\">text</text>", "transformed")]
    [DataRow("<text id=\"target\" filter=\"url(#effect)\">text</text>", "filtered")]
    [DataRow("<text id=\"target\" display=\"none\">text</text>", "Hidden")]
    [DataRow("<text id=\"target\" dx=\"2\">text</text>", "complex text layout")]
    [DataRow("<text id=\"target\" stroke=\"red\">text</text>", "complex text layout")]
    [DataRow("<text id=\"target\" xml:space=\"preserve\">text</text>", "xml:space")]
    public void ComplexAmbiguousOrHiddenTextIsRejected(
        string textMarkup,
        string expectedReason)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><defs><filter id=\"effect\"/></defs><path id=\"p\" d=\"M0 0L10 0\"/>{textMarkup}</svg>";
        SvgDocumentIndex document = Build(source);

        SvgDirectTextAnalysis analysis = _service.Analyze(
            source,
            document,
            Find(document, "target"));

        Assert.IsFalse(analysis.IsEligible);
        StringAssert.Contains(analysis.UnavailableReason!, expectedReason);
    }

    [TestMethod]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><g xml:space=\"preserve\"><text id=\"target\">A   B</text></g></svg>")]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" xml:space=\"preserve\"><text id=\"target\">A   B</text></svg>")]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><g xml:space=\"default\"><text id=\"target\">A   B</text></g></svg>")]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><g xml:space=\"preserve\"><g><g><text id=\"target\">A   B</text></g></g></g></svg>")]
    public void ExplicitXmlSpaceOnAnyAncestorRejectsAnalysisAndCommit(
        string source)
    {
        SvgDocumentIndex document = Build(source);
        SvgElementNode text = Find(document, "target");

        SvgDirectTextAnalysis analysis = _service.Analyze(
            source,
            document,
            text);
        SvgAttributeEditResult edit = _service.CreateEdit(
            source,
            document,
            text,
            "replacement");
        TextDocument unchanged = new(source);
        unchanged.UndoStack.MarkAsOriginalFile();

        Assert.IsFalse(analysis.IsEligible);
        StringAssert.Contains(analysis.UnavailableReason!, "xml:space");
        Assert.IsFalse(edit.IsSuccess);
        Assert.IsNull(edit.Edit);
        Assert.AreEqual(source, unchanged.Text);
        Assert.IsFalse(unchanged.UndoStack.CanUndo);
    }

    [TestMethod]
    public void XmlSpaceOnSiblingDoesNotRejectUnrelatedDirectText()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g xml:space=\"preserve\"><text id=\"sibling\">A   B</text></g><text id=\"target\">Editable</text></svg>";
        SvgDocumentIndex document = Build(source);

        SvgDirectTextAnalysis analysis = _service.Analyze(
            source,
            document,
            Find(document, "target"));

        Assert.IsTrue(analysis.IsEligible, analysis.UnavailableReason);
        Assert.AreEqual("Editable", analysis.Content!.Text);
    }

    [TestMethod]
    public void StaleSpansInvalidSourceOversizeAndInvalidUnicodeFailClosed()
    {
        const string oldSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\">old</text></svg>";
        SvgDocumentIndex oldDocument = Build(oldSource);
        SvgElementNode oldText = Find(oldDocument, "target");
        const string movedSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/><text id=\"target\">old</text></svg>";
        SvgDocumentIndex movedDocument = Build(movedSource);

        SvgDirectTextAnalysis stale = _service.Analyze(
            movedSource,
            movedDocument,
            oldText);
        Assert.IsFalse(stale.IsEligible);
        StringAssert.Contains(stale.UnavailableReason!, "stale");

        SvgAttributeEditResult invalid = _service.CreateEdit(
            oldSource + "<",
            oldDocument,
            oldText,
            "new");
        Assert.IsFalse(invalid.IsSuccess);

        SvgAttributeEditResult oversized = _service.CreateEdit(
            oldSource,
            oldDocument,
            oldText,
            new string('x', SvgVisualTextIndexService.MaximumTextLength + 1));
        Assert.IsFalse(oversized.IsSuccess);

        SvgAttributeEditResult surrogate = _service.CreateEdit(
            oldSource,
            oldDocument,
            oldText,
            "broken\uD800");
        Assert.IsFalse(surrogate.IsSuccess);
    }

    [TestMethod]
    public void CommitIsOneUndoUnitAndCancelCreatesNoEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\">old</text></svg>";
        SvgDocumentIndex documentIndex = Build(source);
        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            documentIndex,
            Find(documentIndex, "target"),
            "متن new");
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        // Candidate typing/cancel never calls the edit service's returned edit.
        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);

        new AvalonEditDocumentEditService().Apply(document, result.Edit!);
        string changed = document.Text;
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
        document.UndoStack.Redo();
        Assert.AreEqual(changed, document.Text);
    }

    private SvgDocumentIndex Build(string source)
    {
        SvgDocumentIndexResult result = _indexService.Build(source);
        Assert.IsNotNull(
            result.Document,
            result.Validation.Message ?? result.IndexError);
        return result.Document;
    }

    private static SvgElementNode Find(
        SvgDocumentIndex document,
        string id) =>
        document.Elements.Single(element => element.Id == id);

    private string ReadText(string source, string id)
    {
        SvgDocumentIndex document = Build(source);
        SvgElementNode text = Find(document, id);
        SvgDirectTextAnalysis analysis = _service.Analyze(
            source,
            document,
            text);
        Assert.IsTrue(analysis.IsEligible, analysis.UnavailableReason);
        return analysis.Content!.Text;
    }
}
