using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTextResizeTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgVisualGeometryIndexService _visualIndexService = new();
    private readonly SvgVisualTextMeasurementService _measurementService = new();
    private readonly SvgVisualResizeHandleService _handleService = new();
    private readonly SvgTextResizeService _resizeService = new();

    [TestMethod]
    public void EligibleMeasuredTextReceivesOneUniformHandle()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"100\" y=\"60\" font-size=\"20px\" text-anchor=\"end\" direction=\"rtl\">سلام</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(35, 40, 100, 64));

        IReadOnlyList<SvgResizeHandleDefinition> handles =
            _handleService.Create(text);

        Assert.IsTrue(text.IsResizable, text.ResizeUnavailableReason);
        Assert.HasCount(1, handles);
        Assert.AreEqual(SvgResizeHandle.Uniform, handles[0].Handle);
        Assert.AreEqual(new SvgVisualPoint(35, 64), handles[0].Point);
        Assert.AreEqual("uniform", SvgVisualResizeHandleService.ToWireName(
            handles[0].Handle));
    }

    [TestMethod]
    public void UniformResizeScalesMeasuredBoundsAroundSourceOwnedAnchor()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"100\" y=\"60\" font-size=\"20px\" text-anchor=\"end\" direction=\"rtl\">سلام</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(35, 40, 100, 64));
        SvgVisualPoint original = _handleService.Create(text).Single().Point;
        SvgVisualPoint anchor = new(100, 60);
        SvgVisualPoint doubled = new(
            anchor.X + (2 * (original.X - anchor.X)),
            anchor.Y + (2 * (original.Y - anchor.Y)));

        Assert.IsTrue(_resizeService.TryCalculate(
            text,
            SvgResizeHandle.Uniform,
            doubled,
            out SvgTextResizeCandidate candidate,
            out string? error), error);

        Assert.AreEqual(40, candidate.FontSize, 0.000001);
        Assert.AreEqual(-30, candidate.Geometry.Bounds.Left, 0.000001);
        Assert.AreEqual(20, candidate.Geometry.Bounds.Top, 0.000001);
        Assert.AreEqual(100, candidate.Geometry.Bounds.Right, 0.000001);
        Assert.AreEqual(68, candidate.Geometry.Bounds.Bottom, 0.000001);
    }

    [TestMethod]
    public void CommitChangesOnlyFontSizeAndPreservesPxBidiAndPosition()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n<text id=\"target\" x=\"100\" y=\"60\" font-size=\"20px\" text-anchor=\"end\" direction=\"rtl\" data-note=\"فارسی\">سلام</text>\r\n</svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(35, 40, 100, 64));

        SvgAttributeEditResult result = _resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            37.5);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string updated = result.Edit!.Apply(source);
        StringAssert.Contains(updated, "font-size=\"37.5px\"");
        StringAssert.Contains(updated, "x=\"100\" y=\"60\"");
        StringAssert.Contains(updated, "text-anchor=\"end\" direction=\"rtl\"");
        StringAssert.Contains(updated, "data-note=\"فارسی\">سلام</text>");
        StringAssert.Contains(updated, "\r\n");
    }

    [TestMethod]
    public void FontSizeIsClampedToDocumentedPositiveBounds()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"100\" y=\"60\" font-size=\"20\">text</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(100, 40, 150, 64));
        SvgVisualPoint anchor = new(100, 60);

        Assert.IsTrue(_resizeService.TryCalculate(
            text,
            SvgResizeHandle.Uniform,
            anchor,
            out SvgTextResizeCandidate minimum,
            out string? error), error);
        Assert.AreEqual(SvgTextResizeService.MinimumFontSize, minimum.FontSize);

        Assert.IsTrue(_resizeService.TryCalculate(
            text,
            SvgResizeHandle.Uniform,
            new SvgVisualPoint(1_000_000, 1_000_000),
            out SvgTextResizeCandidate maximum,
            out error), error);
        Assert.AreEqual(SvgTextResizeService.MaximumFontSize, maximum.FontSize);

        Assert.IsFalse(_resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            0).IsSuccess);
        Assert.IsFalse(_resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            double.NaN).IsSuccess);
        Assert.IsFalse(_resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            SvgTextResizeService.MaximumFontSize + 1).IsSuccess);
    }

    [TestMethod]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\">text</text>",
        "direct font-size",
        true)]
    [DataRow(
        "<g font-size=\"24px\"><text id=\"target\" x=\"1\" y=\"20\">text</text></g>",
        "direct font-size",
        true)]
    [DataRow(
        "<g style=\"font-size:24px\"><text id=\"target\" x=\"1\" y=\"20\">text</text></g>",
        "direct font-size",
        true)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" style=\"font-size:18px\">text</text>",
        "inline style",
        true)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"0.5\">text</text>",
        "1 to 512",
        true)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"513\">text</text>",
        "1 to 512",
        true)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"1000\">text</text>",
        "1 to 512",
        true)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"2em\">text</text>",
        "font-size",
        false)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"NaN\">text</text>",
        "font-size",
        false)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"Infinity\">text</text>",
        "font-size",
        false)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"not-a-size\">text</text>",
        "font-size",
        false)]
    [DataRow(
        "<text id=\"target\" x=\"1\" y=\"20\" font-size=\"16PX\">text</text>",
        "unitless or px",
        true)]
    [DataRow(
        "<g xml:space=\"preserve\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"16\">A   B</text></g>",
        "xml:space",
        false)]
    public void UnsupportedOwnershipNeverGainsAHandleAfterTrustedMeasurement(
        string markup,
        string expectedReason,
        bool receivesMeasuredBounds)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\">{markup}</svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(1, 4, 40, 22));
        SvgAttributeEditResult rejected = _resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            24);
        TextDocument unchanged = new(source);
        unchanged.UndoStack.MarkAsOriginalFile();

        Assert.IsFalse(text.IsResizable);
        Assert.IsEmpty(_handleService.Create(text));
        Assert.IsFalse(rejected.IsSuccess);
        Assert.AreEqual(receivesMeasuredBounds, text.Geometry is not null);
        StringAssert.Contains(
            text.ResizeUnavailableReason ?? text.UnsupportedReason!,
            expectedReason);
        Assert.AreEqual(source, unchanged.Text);
        Assert.IsFalse(unchanged.UndoStack.CanUndo);
    }

    [TestMethod]
    [DataRow("16", "24")]
    [DataRow("16px", "24px")]
    [DataRow("1", "2")]
    [DataRow("512px", "511px")]
    public void DirectSupportedOwnershipRemainsResizableAndPreservesRepresentation(
        string original,
        string expected)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"{original}\">text</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(1, 4, 40, 22));
        double requested = double.Parse(
            expected.TrimEnd('p', 'x'),
            System.Globalization.CultureInfo.InvariantCulture);

        SvgAttributeEditResult result = _resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            requested);

        Assert.IsTrue(text.IsResizable, text.ResizeUnavailableReason);
        Assert.HasCount(1, _handleService.Create(text));
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        StringAssert.Contains(
            result.Edit!.Apply(source),
            $"font-size=\"{expected}\"");
    }

    [TestMethod]
    public void RootXmlSpaceNeverCreatesAResizeHandleAfterTrustedMeasurement()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" xml:space=\"preserve\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"16px\">A   B</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(1, 4, 40, 22));

        SvgAttributeEditResult result = _resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            24);

        Assert.IsFalse(text.IsResizable);
        Assert.IsEmpty(_handleService.Create(text));
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(
            text.ResizeUnavailableReason ?? text.UnsupportedReason!,
            "xml:space");
    }

    [TestMethod]
    public void CommitRevalidatesCurrentDirectFontSizeOwnership()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"16\">text</text></svg>";
        const string changed =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"24\">text</text></svg>";
        SvgVisualElement stale = BuildMeasured(
            source,
            new SvgVisualBounds(1, 4, 40, 22));

        SvgAttributeEditResult result = _resizeService.CreateEdit(
            changed,
            BuildIndex(changed),
            stale,
            32);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage!, "changed");
    }

    [TestMethod]
    public void ResizeCommitIsOneUndoUnitAndCancellationChangesNothing()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"target\" x=\"1\" y=\"20\" font-size=\"16\">text</text></svg>";
        SvgVisualElement text = BuildMeasured(
            source,
            new SvgVisualBounds(1, 4, 40, 22));
        SvgAttributeEditResult result = _resizeService.CreateEdit(
            source,
            BuildIndex(source),
            text,
            24);
        TextDocument document = new(source);
        document.UndoStack.MarkAsOriginalFile();

        // A cancelled candidate gesture never applies its pending edit.
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

    private SvgVisualElement BuildMeasured(
        string source,
        SvgVisualBounds bounds) =>
        _measurementService.Apply(
            BuildDocument(source),
            [new SvgVisualTextMeasurementResult(0, true, bounds)])
        .Elements.Single(element => element.SourceElement.Id == "target");

    private SvgVisualElement BuildPending(string source) =>
        BuildDocument(source).Elements.Single(element =>
            element.SourceElement.Id == "target");

    private SvgVisualDocument BuildDocument(string source)
    {
        SvgDocumentIndex index = BuildIndex(source);
        return _visualIndexService.Build(
            index,
            new SvgCanvasSizeReader().Read(source),
            source);
    }

    private SvgDocumentIndex BuildIndex(string source)
    {
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsNotNull(index.Document, index.Validation.Message ?? index.IndexError);
        return index.Document;
    }
}
