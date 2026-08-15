using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

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
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"target\" x=\"40\" y=\"25\" width=\"20\" height=\"20\"/></svg>";
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
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"0\" y=\"20\" width=\"10\" height=\"10\"/><rect id=\"near\" x=\"23\" y=\"20\" width=\"10\" height=\"10\"/><rect id=\"far\" x=\"24\" y=\"20\" width=\"10\" height=\"10\"/></svg>";
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
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect id=\"moving\" x=\"30\" y=\"20\" width=\"10\" height=\"10\"/><rect id=\"left\" x=\"24\" y=\"20\" width=\"3\" height=\"3\"/><rect id=\"right\" x=\"43\" y=\"20\" width=\"3\" height=\"3\"/></svg>";
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
    [DataRow("Fit", 1.25d, 1.25d)]
    [DataRow("50%", 2d, 2d)]
    [DataRow("75%", 1.3333333333333333d, 1.3333333333333333d)]
    [DataRow("100%", 1d, 1d)]
    [DataRow("200%", 0.5d, 0.5d)]
    public void DistantAxisAlignedTargetIsRejectedAtEveryZoomScale(
        string zoomMode,
        double svgUnitsPerCssPixelX,
        double svgUnitsPerCssPixelY)
    {
        _ = zoomMode;
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 200\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"vertically-distant\" x=\"40\" y=\"150\" width=\"20\" height=\"20\"/><rect id=\"horizontally-distant\" x=\"250\" y=\"40\" width=\"20\" height=\"20\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult horizontalResult = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "vertically-distant")],
            document.Viewport,
            requestedDeltaX: 8,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX,
            svgUnitsPerCssPixelY);
        SvgSnapResult verticalResult = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "horizontally-distant")],
            document.Viewport,
            requestedDeltaX: 0,
            requestedDeltaY: 8,
            svgUnitsPerCssPixelX,
            svgUnitsPerCssPixelY);

        Assert.AreEqual(8, horizontalResult.DeltaX);
        Assert.AreEqual(0, horizontalResult.Guides.Count);
        Assert.AreEqual(8, verticalResult.DeltaY);
        Assert.AreEqual(0, verticalResult.Guides.Count);
    }

    [TestMethod]
    [DataRow("Fit", 1.25d)]
    [DataRow("50%", 2d)]
    [DataRow("75%", 1.3333333333333333d)]
    [DataRow("100%", 1d)]
    [DataRow("200%", 0.5d)]
    public void EquivalentCssSpaceGeometrySnapsAtEveryZoomScale(
        string zoomMode,
        double svgUnitsPerCssPixel)
    {
        _ = zoomMode;
        double size = 10 * svgUnitsPerCssPixel;
        double targetX = 20 * svgUnitsPerCssPixel;
        double targetY = 20 * svgUnitsPerCssPixel;
        string source = FormattableString.Invariant(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {200 * svgUnitsPerCssPixel} {100 * svgUnitsPerCssPixel}\"><rect id=\"moving\" x=\"0\" y=\"0\" width=\"{size}\" height=\"{size}\"/><rect id=\"target\" x=\"{targetX}\" y=\"{targetY}\" width=\"{size}\" height=\"{size}\"/></svg>");
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "target")],
            document.Viewport,
            requestedDeltaX: 8 * svgUnitsPerCssPixel,
            requestedDeltaY: 0,
            svgUnitsPerCssPixel,
            svgUnitsPerCssPixel);

        Assert.AreEqual(
            10 * svgUnitsPerCssPixel,
            result.DeltaX,
            0.0000001);
        Assert.AreEqual(1, result.Guides.Count);
        Assert.AreEqual(
            PreviewAlignmentGuideOrientation.Vertical,
            result.Guides[0].Orientation);
    }

    [TestMethod]
    public void OrthogonalAllowanceIncludesTwelveCssPixelsAndRejectsBeyondIt()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 120\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"boundary\" x=\"40\" y=\"54\" width=\"20\" height=\"20\"/><rect id=\"outside\" x=\"40\" y=\"54.02\" width=\"20\" height=\"20\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult boundary = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "boundary")],
            document.Viewport,
            requestedDeltaX: 9,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 0.5,
            svgUnitsPerCssPixelY: 2);
        SvgSnapResult outside = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "outside")],
            document.Viewport,
            requestedDeltaX: 9,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 0.5,
            svgUnitsPerCssPixelY: 2);

        Assert.AreEqual(10, boundary.DeltaX);
        Assert.AreEqual(1, boundary.Guides.Count);
        Assert.AreEqual(9, outside.DeltaX);
        Assert.AreEqual(0, outside.Guides.Count);
    }

    [TestMethod]
    public void OrthogonallyNearTargetUsesIndependentAxisScale()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"near\" x=\"40\" y=\"50\" width=\"20\" height=\"20\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "near")],
            document.Viewport,
            requestedDeltaX: 9,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 0.5,
            svgUnitsPerCssPixelY: 2);

        Assert.AreEqual(10, result.DeltaX);
        Assert.AreEqual(
            40,
            result.Guides.Single(guide =>
                guide.Orientation
                    == PreviewAlignmentGuideOrientation.Vertical).Position);
    }

    [TestMethod]
    public void AlreadyAlignedAxisDoesNotEmitAnUnappliedGuide()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect id=\"moving\" x=\"0\" y=\"10\" width=\"10\" height=\"10\"/><rect id=\"target\" x=\"30\" y=\"10\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "target")],
            document.Viewport,
            requestedDeltaX: 20,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);

        Assert.AreEqual(20, result.DeltaX);
        Assert.AreEqual(0, result.Guides.Count);
    }

    [TestMethod]
    public void SubHundredthCssCorrectionAndDisabledSnappingEmitNoGuides()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect id=\"moving\" x=\"0\" y=\"10\" width=\"10\" height=\"10\"/><rect id=\"target\" x=\"30.009\" y=\"10\" width=\"10\" height=\"10\"/></svg>";
        SvgVisualDocument document = Build(source);
        SvgVisualElement moving = ById(document, "moving");
        SvgVisualElement target = ById(document, "target");

        SvgSnapResult subPixel = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            requestedDeltaX: 20,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);
        SvgSnapResult disabled = _service.Snap(
            [moving],
            [target],
            document.Viewport,
            requestedDeltaX: 18,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1,
            isEnabled: false);

        Assert.AreEqual(20, subPixel.DeltaX);
        Assert.AreEqual(0, subPixel.Guides.Count);
        Assert.AreEqual(18, disabled.DeltaX);
        Assert.AreEqual(0, disabled.Guides.Count);
    }

    [TestMethod]
    public void AppliedCorrectionsEmitAtMostOneGuidePerAxis()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect id=\"moving\" x=\"10\" y=\"10\" width=\"20\" height=\"20\"/><rect id=\"target\" x=\"40\" y=\"40\" width=\"20\" height=\"20\"/></svg>";
        SvgVisualDocument document = Build(source);

        SvgSnapResult result = _service.Snap(
            [ById(document, "moving")],
            [ById(document, "target")],
            document.Viewport,
            requestedDeltaX: 9,
            requestedDeltaY: 9,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);

        Assert.AreEqual(10, result.DeltaX);
        Assert.AreEqual(10, result.DeltaY);
        Assert.AreEqual(2, result.Guides.Count);
        Assert.IsTrue(result.Guides
            .GroupBy(guide => guide.Orientation)
            .All(group => group.Count() == 1));
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
        DocumentInspectorViewModel inspector = new();
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
    public void HostPoliciesFilterLockedHiddenUnsafeAndIncompatibleCandidatesBeforeRanking()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"background\" x=\"0\" y=\"0\" width=\"200\" height=\"100\"/><rect id=\"moving\" x=\"10\" y=\"10\" width=\"10\" height=\"10\"/><rect id=\"locked\" x=\"31\" y=\"40\" width=\"10\" height=\"10\"/><rect id=\"hidden\" x=\"32\" y=\"50\" width=\"10\" height=\"10\" display=\"none\"/><rect id=\"unsafe\" x=\"33\" y=\"60\" width=\"10\" height=\"10\" transform=\"translate(1 0)\"/><rect id=\"invalid\" x=\"33\" y=\"70\" width=\"-1\" height=\"10\"/><rect id=\"eligible\" x=\"34\" y=\"10\" width=\"10\" height=\"10\"/><g id=\"other-parent\"><rect id=\"incompatible\" x=\"30\" y=\"30\" width=\"10\" height=\"10\"/></g></svg>";
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
        SvgLayerViewModel lockedLayer = inspector.LayerRoots.Single(layer =>
            layer.Element.Id == "locked");
        Assert.IsTrue(inspector.ToggleLayerLock(lockedLayer));

        SvgVisualElement moving = ById(visual, "moving");
        SvgElementNode parent = index.FindParent(moving.SourceElement)!;
        SvgVisualElement[] hostCandidates = visual.Elements
            .Where(element => ReferenceEquals(
                    index.FindParent(element.SourceElement),
                    parent)
                && element.IsMovable
                && element.Geometry is not null
                && !inspector.IsElementEffectivelyLocked(element.SourceElement)
                && inspector.IsElementEffectivelyVisible(element.SourceElement))
            .ToArray();

        Assert.IsFalse(hostCandidates.Any(element =>
            element.SourceElement.Id is "locked" or "hidden" or "unsafe"
                or "invalid" or "incompatible"));
        Assert.IsTrue(hostCandidates.Any(element =>
            element.SourceElement.Id == "moving"));
        Assert.IsTrue(hostCandidates.Any(element =>
            element.SourceElement.Id == "background"));
        Assert.IsTrue(hostCandidates.Any(element =>
            element.SourceElement.Id == "eligible"));

        SvgSnapResult result = _service.Snap(
            [moving],
            hostCandidates,
            visual.Viewport,
            requestedDeltaX: 10,
            requestedDeltaY: 0,
            svgUnitsPerCssPixelX: 1,
            svgUnitsPerCssPixelY: 1);

        Assert.AreEqual(14, result.DeltaX);
        Assert.AreEqual(34, result.Guides.Single(guide =>
            guide.Orientation
                == PreviewAlignmentGuideOrientation.Vertical).Position);
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
