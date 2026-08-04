using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgOpacityServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgOpacityService _service = new();

    [TestMethod]
    [DataRow("0", 0d)]
    [DataRow("0.375", 37.5d)]
    [DataRow("1", 100d)]
    public void ExistingUnitOpacityMapsToPercentage(
        string sourceValue,
        double expectedPercent)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" opacity=\"{sourceValue}\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgOpacityControlState state = _service.Analyze(
            document,
            Find(document),
            source);

        Assert.IsTrue(state.IsEnabled, state.UnavailableReason);
        Assert.AreEqual(expectedPercent, state.Percent, 0.0001);
    }

    [TestMethod]
    public void MissingOpacityDefaultsToOneHundredAndCommitAddsInvariantValue()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" fill-opacity=\"0.2\" stroke-opacity=\"0.3\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode element = Find(document);

        SvgOpacityControlState state = _service.Analyze(document, element);
        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            element,
            37.5);

        Assert.IsTrue(state.IsEnabled);
        Assert.AreEqual(100, state.Percent);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        StringAssert.Contains(candidate, "opacity=\"0.375\"");
        StringAssert.Contains(candidate, "fill-opacity=\"0.2\"");
        StringAssert.Contains(candidate, "stroke-opacity=\"0.3\"");
    }

    [TestMethod]
    public void OneHundredPercentRemovesOnlyOptionalOpacity()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"shape\" opacity=\"0.4\" fill-opacity=\"0.5\">سلام</text></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document),
            100);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        Assert.IsFalse(candidate.Contains(" opacity=", StringComparison.Ordinal));
        StringAssert.Contains(candidate, "fill-opacity=\"0.5\"");
        StringAssert.Contains(candidate, "سلام");
    }

    [TestMethod]
    public void ZeroPercentRemainsIndexableButBecomesNonHitTestable()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><rect id=\"shape\" x=\"0\" y=\"0\" width=\"20\" height=\"20\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document),
            0);
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            rebuilt,
            new SvgCanvasSize(100, 100),
            candidate);

        Assert.IsNotNull(rebuilt.Elements.Single(element => element.Id == "shape"));
        Assert.IsNull(new SvgVisualHitTestService().HitTest(
            visual,
            new SvgMappedPreviewPoint(new SvgVisualPoint(10, 10), 1)));
    }

    [TestMethod]
    public void StyleAnimationMalformedValueAndEffectsDisableControl()
    {
        string[] fragments =
        [
            "style=\"opacity: .5\"",
            "opacity=\"50%\"",
            "filter=\"url(#f)\"",
            "transform=\"translate(1 1)\"",
            "><animate attributeName=\"opacity\"/></rect"
        ];

        foreach (string fragment in fragments)
        {
            string source = fragment.StartsWith('>')
                ? $"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"{fragment}></svg>"
                : $"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" {fragment}/></svg>";
            SvgDocumentIndex document = _indexService.Build(source).Document!;

            SvgOpacityControlState state = _service.Analyze(document, Find(document));

            Assert.IsTrue(state.IsVisible);
            Assert.IsFalse(state.IsEnabled, fragment);
        }
    }

    [TestMethod]
    public void AncestorTransformDisablesQuickOpacityControl()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g transform=\"translate(1 1)\"><rect id=\"shape\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgOpacityControlState state = _service.Analyze(
            document,
            Find(document),
            source);

        Assert.IsFalse(state.IsEnabled);
        StringAssert.Contains(state.UnavailableReason, "transformed");
    }

    [TestMethod]
    public void ViewModelParsesPercentAndRevertsCandidateWithoutEditingSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle id=\"shape\" opacity=\"0.8\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode element = Find(document);
        SvgOpacityViewModel viewModel = new(
            element,
            _service.Analyze(document, element));

        viewModel.Percent = 22;
        Assert.AreEqual("22", viewModel.Text);
        Assert.IsTrue(viewModel.TryReadPercent(out double percent));
        Assert.AreEqual(22, percent);
        viewModel.Revert();

        Assert.AreEqual(80, viewModel.Percent, 0.0001);
        Assert.AreEqual("80", viewModel.Text);
    }

    [TestMethod]
    public void CommitAttemptDeduplicatesEnterThenFocusLossAndPersianStaysExact()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"shape\">سلام دنیا</text></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode element = Find(document);
        SvgOpacityViewModel viewModel = new(
            element,
            _service.Analyze(document, element, source))
        {
            Text = "50"
        };

        viewModel.MarkCommitAttempt();
        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            element,
            50);

        Assert.IsTrue(viewModel.WasCurrentTextAlreadyAttempted);
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        StringAssert.Contains(candidate, "opacity=\"0.5\"");
        StringAssert.Contains(candidate, "سلام دنیا");
    }

    [TestMethod]
    public void AppliedOpacityEditIsOneUndoUnit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAttributeEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document),
            25);
        ICSharpCode.AvalonEdit.Document.TextDocument textDocument = new(source);

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        textDocument.UndoStack.Undo();

        Assert.AreEqual(source, textDocument.Text);
    }

    [TestMethod]
    public void InspectOnlyPathRequiresReliableBounds()
    {
        const string bounded =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><path id=\"shape\" d=\"M10 10 L40 40\"/></svg>";
        const string unbounded =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><path id=\"shape\" d=\"M10 10 A20 20 0 0 1 40 40\"/></svg>";
        SvgDocumentIndex boundedIndex = _indexService.Build(bounded).Document!;
        SvgDocumentIndex unboundedIndex = _indexService.Build(unbounded).Document!;

        Assert.IsTrue(_service.Analyze(
            boundedIndex,
            Find(boundedIndex),
            bounded).IsEnabled);
        Assert.IsFalse(_service.Analyze(
            unboundedIndex,
            Find(unboundedIndex),
            unbounded).IsEnabled);
    }

    private static SvgElementNode Find(SvgDocumentIndex document) =>
        document.Elements.Single(element => element.Id == "shape");
}
