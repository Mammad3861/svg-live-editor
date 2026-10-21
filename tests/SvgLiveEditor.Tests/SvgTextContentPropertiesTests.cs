using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTextContentPropertiesTests
{
    private readonly SvgDocumentIndexService _indexService = new();

    [TestMethod]
    public void DirectTextPropertiesExposeExactDecodedContent()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"2\" y=\"20\">  سلام &amp; SVG  </text></svg>";
        SvgDocumentIndex document = Build(source);
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);

        inspector.SelectNode(document.Elements.Single(element =>
            element.Id == "label"));

        Assert.IsTrue(inspector.HasTextContentControl);
        Assert.IsNotNull(inspector.TextContent);
        Assert.IsTrue(inspector.TextContent.IsEnabled);
        Assert.AreEqual("  سلام & SVG  ", inspector.TextContent.Value);
    }

    [TestMethod]
    public void UnsupportedNestedTextRemainsVisibleButReadOnlyWithReason()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\"><tspan>سلام</tspan></text></svg>";
        SvgDocumentIndex document = Build(source);
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);

        inspector.SelectNode(document.Elements.Single(element =>
            element.Id == "label"));

        Assert.IsTrue(inspector.HasTextContentControl);
        Assert.IsFalse(inspector.TextContent!.IsEnabled);
        StringAssert.Contains(inspector.TextContent.UnavailableReason, "tspan");
    }

    [TestMethod]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" xml:space=\"preserve\"><text id=\"label\">A   B</text></svg>")]
    [DataRow(
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><g xml:space=\"default\"><text id=\"label\">A   B</text></g></svg>")]
    public void InheritedXmlSpaceKeepsPropertiesTextContentReadOnly(
        string source)
    {
        SvgDocumentIndex document = Build(source);
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);

        inspector.SelectNode(document.Elements.Single(element =>
            element.Id == "label"));

        Assert.IsTrue(inspector.HasTextContentControl);
        Assert.IsFalse(inspector.TextContent!.IsEnabled);
        StringAssert.Contains(
            inspector.TextContent.UnavailableReason,
            "xml:space");
    }

    [TestMethod]
    public void SessionLockDisablesTheSharedTextControl()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"><text id=\"label\">سلام</text></g></svg>";
        SvgDocumentIndex document = Build(source);
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);
        SvgLayerViewModel group = inspector.LayerRoots.Single();
        Assert.IsTrue(inspector.ToggleLayerLock(group));

        inspector.SelectNode(document.Elements.Single(element =>
            element.Id == "label"));

        Assert.IsFalse(inspector.TextContent!.IsEnabled);
        StringAssert.Contains(inspector.TextContent.UnavailableReason, "Unlock");
    }

    [TestMethod]
    public void CandidateCancelCommitAndRefreshAreLocalUntilProductionCommit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\">old</text></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode element = document.Elements.Single(item =>
            item.Id == "label");
        SvgDirectTextAnalysis analysis = new SvgDirectTextContentEditService()
            .Analyze(source, document, element);
        SvgTextContentViewModel field = new(element, analysis, isSessionLocked: false);

        field.Value = "candidate";
        Assert.IsTrue(field.HasUncommittedValue);
        Assert.AreEqual("old", field.OriginalValue);
        StringAssert.Contains(source, ">old<");
        field.Revert();
        Assert.AreEqual("old", field.Value);
        Assert.IsFalse(field.HasUncommittedValue);

        field.Value = "committed";
        field.MarkCommitAttempt();
        Assert.IsTrue(field.WasCurrentValueAlreadyAttempted);
        field.MarkApplied();
        Assert.AreEqual("committed", field.OriginalValue);
        Assert.IsFalse(field.HasUncommittedValue);

        DocumentInspectorViewModel refreshed = new();
        refreshed.Load(document, preferredSelection: null, source: source);
        refreshed.SelectNode(element);
        Assert.AreEqual("old", refreshed.TextContent!.Value);
        Assert.IsFalse(refreshed.TextContent.HasUncommittedValue);
    }

    private SvgDocumentIndex Build(string source)
    {
        SvgDocumentIndexResult result = _indexService.Build(source);
        Assert.IsNotNull(result.Document, result.Validation.Message ?? result.IndexError);
        return result.Document;
    }
}
