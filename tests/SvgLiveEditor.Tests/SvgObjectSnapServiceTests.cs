using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgObjectSnapServiceTests
{
    private const string Token = "00112233445566778899AABBCCDDEEFF";
    private readonly SvgObjectSnapService _service = new();

    [TestMethod]
    public void EdgeCenterAndCanvasTargetsSnapPerAxis()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"target\" x=\"40\" y=\"50\" width=\"20\" height=\"20\"/></svg>";
        SvgVisualDocument document = Build(source);
        SvgVisualElement moving = ById(document, "moving");
        SvgVisualElement target = ById(document, "target");

        SvgSnapResult edge = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            8,
            0,
            1,
            1);
        Assert.AreEqual(10, edge.DeltaX);
        Assert.AreEqual(40, edge.Guides.Single(
            guide => guide.Orientation
                == PreviewAlignmentGuideOrientation.Vertical).Position);

        SvgSnapResult center = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            48,
            0,
            1,
            1);
        Assert.AreEqual(50, center.DeltaX);

        SvgSnapResult canvas = _service.Snap(
            [moving],
            [],
            document.Viewport,
            78,
            0,
            1,
            1);
        Assert.AreEqual(80, canvas.DeltaX);
        Assert.AreEqual(100, canvas.Guides.Single().Position);
    }

    [TestMethod]
    public void ThresholdIsDefinedInCssPixelsAndMappedThroughZoom()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"target\" x=\"30\" y=\"0\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument document = Build(source);
        SvgVisualElement moving = ById(document, "moving");
        SvgVisualElement target = ById(document, "target");

        SvgSnapResult exactBoundary = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            12,
            0,
            svgUnitsPerCssPixelX: 2,
            svgUnitsPerCssPixelY: 2);
        Assert.AreEqual(20, exactBoundary.DeltaX);

        SvgSnapResult outside = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            11.9,
            0,
            svgUnitsPerCssPixelX: 2,
            svgUnitsPerCssPixelY: 2);
        Assert.AreEqual(11.9, outside.DeltaX);
        Assert.IsFalse(outside.Guides.Any(guide =>
            guide.Orientation == PreviewAlignmentGuideOrientation.Vertical));

        PreviewSvgCoordinateMapper mapper = new();
        Assert.IsTrue(mapper.TryMap(
            document.Viewport,
            new PreviewImageMetrics(0, 0, 400, 200),
            new SvgVisualPoint(100, 50),
            out SvgMappedPreviewPoint zoomed));
        Assert.AreEqual(0.5, zoomed.SvgUnitsPerCssPixelX, 0.0001);
        Assert.AreEqual(0.5, zoomed.SvgUnitsPerCssPixelY, 0.0001);
    }

    [TestMethod]
    public void NearestCorrectionWinsAndUnrelatedTiesAreSuppressed()
    {
        const string nearestSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"0\" y=\"20\" width=\"10\" height=\"10\"/><rect id=\"near\" x=\"23\" y=\"60\" width=\"10\" height=\"10\"/><rect id=\"far\" x=\"24\" y=\"80\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument nearestDocument = Build(nearestSource);
        SvgSnapResult nearest = _service.Snap(
            [ById(nearestDocument, "moving")],
            [ById(nearestDocument, "near"), ById(nearestDocument, "far")],
            nearestDocument.Viewport,
            requestedDeltaX: 10,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);
        Assert.AreEqual(13, nearest.DeltaX);
        Assert.AreEqual(23, nearest.Guides.Single(guide =>
            guide.Orientation
                == PreviewAlignmentGuideOrientation.Vertical).Position);

        const string tiedSource =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect id=\"moving\" x=\"30\" y=\"20\" width=\"10\" height=\"10\"/><rect id=\"left\" x=\"24\" y=\"60\" width=\"3\" height=\"3\"/><rect id=\"right\" x=\"43\" y=\"80\" width=\"3\" height=\"3\"/></svg>";
        SvgVisualDocument tiedDocument = Build(tiedSource);
        SvgSnapResult tied = _service.Snap(
            [ById(tiedDocument, "moving")],
            [ById(tiedDocument, "left"), ById(tiedDocument, "right")],
            tiedDocument.Viewport,
            0,
            0,
            1,
            1);
        Assert.AreEqual(0, tied.DeltaX);
        Assert.IsFalse(tied.Guides.Any(guide =>
            guide.Orientation
                == PreviewAlignmentGuideOrientation.Vertical));
    }

    [TestMethod]
    public void FullCanvasSiblingIsNotTreatedAsAnObjectTarget()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"background\" x=\"0\" y=\"0\" width=\"200\" height=\"100\"/><rect id=\"moving\" x=\"10\" y=\"10\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "background")],
            document.Viewport,
            requestedDeltaX: -8,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);

        Assert.AreEqual(-8, result.DeltaX);
        Assert.IsFalse(result.Guides.Any(guide =>
            guide.Orientation
                == PreviewAlignmentGuideOrientation.Vertical));
    }

    [TestMethod]
    public void InspectorVisibilityPolicyIdentifiesHiddenSnapCandidates()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shown\"/><rect id=\"hidden\" display=\"none\"/></svg>";
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            index,
            new SvgCanvasSizeReader().Read(source),
            source);
        SvgLiveEditor.ViewModels.DocumentInspectorViewModel inspector = new();
        inspector.Load(
            index,
            preferredSelection: null,
            source: source,
            visualDocument: visual);

        Assert.IsTrue(inspector.IsElementEffectivelyVisible(
            index.Elements.Single(element => element.Id == "shown")));
        Assert.IsFalse(inspector.IsElementEffectivelyVisible(
            index.Elements.Single(element => element.Id == "hidden")));
    }

    [TestMethod]
    public void MovingSelectionIsExcludedFromSiblingTargets()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\" x=\"0\" y=\"0\" width=\"10\" height=\"10\"/><rect id=\"b\" x=\"20\" y=\"0\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            document.Elements,
            document.Elements,
            document.Viewport,
            3,
            4,
            1,
            1);

        Assert.AreEqual(3, result.DeltaX);
        Assert.AreEqual(4, result.DeltaY);
        Assert.AreEqual(0, result.Guides.Count);
    }

    [TestMethod]
    public void HostOverlaySchemaBoundsSelectionsAndGuidesAndKeepsPngPathSeparate()
    {
        PreviewPageMessageBuilder builder = new();
        PreviewVisualSelection primary = Selection("00", true);
        PreviewVisualSelection secondary = Selection("11", false);
        string json = builder.BuildVisualOverlayMessage(
            Token,
            5,
            [primary, secondary],
            [
                new PreviewAlignmentGuide(
                    PreviewAlignmentGuideOrientation.Vertical,
                    20,
                    0,
                    100),
                new PreviewAlignmentGuide(
                    PreviewAlignmentGuideOrientation.Horizontal,
                    30,
                    0,
                    200)
            ]);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.AreEqual(2, document.RootElement
            .GetProperty("selections").GetArrayLength());
        Assert.AreEqual(2, document.RootElement
            .GetProperty("guides").GetArrayLength());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            builder.BuildVisualOverlayMessage(
                Token,
                5,
                [primary, secondary with { IsPrimary = true }],
                []));

        string html = new PreviewHtmlBuilder().Build(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"/>",
            300,
            150,
            Token,
            PreviewViewportPosition.Center,
            5,
            new SvgVisualViewport(
                0,
                0,
                300,
                150,
                SvgPreserveAspectRatio.Default));
        StringAssert.Contains(html, "context.drawImage(image, 0, 0");
        Assert.IsFalse(html.Contains(
            "drawImage(selectionOverlay",
            StringComparison.Ordinal));
    }

    private static PreviewVisualSelection Selection(
        string prefix,
        bool primary) => new(
        SvgVisualElementKind.Rect,
        new SvgVisualShapeGeometry(
            SvgVisualElementKind.Rect,
            1,
            2,
            3,
            4),
        0,
        0,
        prefix.PadRight(32, prefix[0]),
        [],
        primary);

    private static SvgVisualDocument Build(string source)
    {
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        return new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
    }

    private static SvgVisualElement ById(
        SvgVisualDocument document,
        string id) => document.Elements.Single(
            element => element.SourceElement.Id == id);
}
