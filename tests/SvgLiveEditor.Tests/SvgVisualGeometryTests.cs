using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualGeometryTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgVisualGeometryIndexService _visualIndexService = new();
    private readonly PreviewSvgCoordinateMapper _coordinateMapper = new();

    [TestMethod]
    public void PointerMapping_AccountsForNonZeroViewBoxAndFitScale()
    {
        SvgVisualViewport viewport = new(
            100,
            200,
            400,
            200,
            SvgPreserveAspectRatio.Default);

        Assert.IsTrue(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(20, 30, 800, 400),
            new SvgVisualPoint(420, 230),
            out SvgMappedPreviewPoint mapped));

        Assert.AreEqual(300, mapped.Point.X, 0.0001);
        Assert.AreEqual(300, mapped.Point.Y, 0.0001);
        Assert.AreEqual(3, mapped.HitTolerance, 0.0001);
    }

    [TestMethod]
    public void PointerMapping_RejectsMeetLetterboxAndTracksScrollOffset()
    {
        SvgVisualViewport viewport = new(
            0,
            0,
            100,
            100,
            SvgPreserveAspectRatio.Default);

        Assert.IsFalse(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(-200, -40, 400, 200),
            new SvgVisualPoint(-175, 20),
            out _));
        Assert.IsTrue(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(-200, -40, 400, 200),
            new SvgVisualPoint(0, 60),
            out SvgMappedPreviewPoint mapped));

        Assert.AreEqual(50, mapped.Point.X, 0.0001);
        Assert.AreEqual(50, mapped.Point.Y, 0.0001);
    }

    [TestMethod]
    public void PointerMapping_PreserveAspectRatioNoneUsesBothAxes()
    {
        SvgVisualViewport viewport = new(
            -50,
            10,
            200,
            100,
            new SvgPreserveAspectRatio(
                true,
                0,
                0,
                false,
                "none"));

        Assert.IsTrue(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(0, 0, 800, 200),
            new SvgVisualPoint(400, 100),
            out SvgMappedPreviewPoint mapped));

        Assert.AreEqual(50, mapped.Point.X, 0.0001);
        Assert.AreEqual(60, mapped.Point.Y, 0.0001);
    }

    [TestMethod]
    public void PointerMapping_RemainsAlignedAfterManualZoomPanAndResize()
    {
        SvgVisualViewport viewport = new(
            0,
            0,
            200,
            100,
            SvgPreserveAspectRatio.Default);

        Assert.IsTrue(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(50, 25, 400, 200),
            new SvgVisualPoint(250, 125),
            out SvgMappedPreviewPoint fit));
        Assert.IsTrue(_coordinateMapper.TryMap(
            viewport,
            new PreviewImageMetrics(-350, -175, 800, 400),
            new SvgVisualPoint(50, 25),
            out SvgMappedPreviewPoint zoomedAndPanned));

        Assert.AreEqual(fit.Point.X, zoomedAndPanned.Point.X, 0.0001);
        Assert.AreEqual(fit.Point.Y, zoomedAndPanned.Point.Y, 0.0001);
        Assert.IsTrue(zoomedAndPanned.HitTolerance < fit.HitTolerance);
    }

    [TestMethod]
    public void HitTesting_SelectsTopmostSupportedElement()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="bottom" x="10" y="10" width="50" height="50" />
              <circle id="top" cx="35" cy="35" r="20" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        SvgVisualElement? hit = new SvgVisualHitTestService().HitTest(
            document,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(35, 35),
                1));

        Assert.AreEqual("top", hit?.SourceElement.Id);
    }

    [TestMethod]
    public void DefinitelyHiddenTopmostElementDoesNotCaptureVisibleHit()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="bottom" x="10" y="10" width="50" height="50" />
              <rect id="hidden" x="10" y="10" width="50" height="50"
                    display="none" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        SvgVisualElement? hit = new SvgVisualHitTestService().HitTest(
            document,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(35, 35),
                1));

        Assert.AreEqual("bottom", hit?.SourceElement.Id);
        Assert.IsFalse(document.Elements.Any(element =>
            element.SourceElement.Id == "hidden"));
    }

    [TestMethod]
    public void EntityEncodedHiddenStateMatchesBrowserXmlDecoding()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="bottom" x="10" y="10" width="50" height="50" />
              <rect id="hidden" x="10" y="10" width="50" height="50"
                    display="n&#111;ne" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        SvgVisualElement? hit = new SvgVisualHitTestService().HitTest(
            document,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(35, 35),
                1));

        Assert.AreEqual("bottom", hit?.SourceElement.Id);
        Assert.IsFalse(document.Elements.Any(element =>
            element.SourceElement.Id == "hidden"));
    }

    [TestMethod]
    public void UnsupportedTopmostElementBlocksLowerVisualHit()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="bottom" x="10" y="10" width="50" height="50" />
              <rect id="unsupported" x="10" y="10" width="50" height="50"
                    transform="translate(0)" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        SvgVisualElement? hit = new SvgVisualHitTestService().HitTest(
            document,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(35, 35),
                1));

        Assert.IsNull(hit);
    }

    [TestMethod]
    public void IndexSupportsRequiredShapesAndNonZeroViewBox()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 viewBox="-20 30 400 200"
                 preserveAspectRatio="xMaxYMin slice">
              <rect x="1" y="2" width="30" height="40" />
              <circle cx="10" cy="20" r="5" />
              <ellipse cx="50" cy="60" rx="7" ry="8" />
              <line x1="1" y1="2" x2="30" y2="40" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        Assert.AreEqual(4, document.Elements.Count);
        Assert.IsTrue(document.Elements.All(element => element.IsMovable));
        Assert.AreEqual(-20, document.Viewport.MinX);
        Assert.AreEqual(30, document.Viewport.MinY);
        Assert.AreEqual(400, document.Viewport.Width);
        Assert.IsTrue(document.Viewport.PreserveAspectRatio.IsSlice);
        Assert.AreEqual(1, document.Viewport.PreserveAspectRatio.AlignX);
        Assert.AreEqual(0, document.Viewport.PreserveAspectRatio.AlignY);
    }

    [TestMethod]
    public void UnsupportedUnitsAndTransformsAreNotHitTestable()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="percent" x="10%" y="1" width="20" height="20" />
              <g transform="translate(10 10)">
                <circle id="transformed" cx="20" cy="20" r="10" />
              </g>
            </svg>
            """;
        SvgVisualDocument document = Build(source);

        SvgVisualElement percent = document.Elements.Single(element =>
            element.SourceElement.Id == "percent");
        SvgVisualElement transformed = document.Elements.Single(element =>
            element.SourceElement.Id == "transformed");
        Assert.IsFalse(percent.IsMovable);
        StringAssert.Contains(percent.UnsupportedReason, "unitless or px");
        Assert.IsFalse(transformed.IsMovable);
        StringAssert.Contains(transformed.UnsupportedReason, "transformed");
        Assert.IsNull(new SvgVisualHitTestService().HitTest(
            document,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(20, 20),
                2)));
    }

    [TestMethod]
    public void MalformedViewportAndUnsupportedCanvasUnitsDisableMovement()
    {
        const string malformedViewBox = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 nope 100">
              <rect id="box" x="1" y="2" width="20" height="20" />
            </svg>
            """;
        const string unsupportedCanvas = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10cm" height="100">
              <rect id="box" x="1" y="2" width="20" height="20" />
            </svg>
            """;

        SvgVisualElement malformed = Build(malformedViewBox).Elements.Single();
        SvgVisualElement unsupported =
            Build(unsupportedCanvas).Elements.Single();

        Assert.IsFalse(malformed.IsMovable);
        StringAssert.Contains(malformed.UnsupportedReason, "viewBox");
        Assert.IsFalse(unsupported.IsMovable);
        StringAssert.Contains(
            unsupported.UnsupportedReason,
            "canvas dimensions");
    }

    [TestMethod]
    public void StyledTransformsAndVisualEffectsDisableMovement()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="transform"
                    style="fill: red; transform : translate(1px)"
                    x="1" y="2" width="20" height="20" />
              <g style="clip-path: url(#clip)">
                <circle id="clipped" cx="20" cy="20" r="10" />
              </g>
            </svg>
            """;

        SvgVisualDocument document = Build(source);
        SvgVisualElement transformed = document.Elements.Single(element =>
            element.SourceElement.Id == "transform");
        SvgVisualElement clipped = document.Elements.Single(element =>
            element.SourceElement.Id == "clipped");

        Assert.IsFalse(transformed.IsMovable);
        StringAssert.Contains(transformed.UnsupportedReason, "transformed");
        Assert.IsFalse(clipped.IsMovable);
        StringAssert.Contains(clipped.UnsupportedReason, "clipped");
    }

    [TestMethod]
    public void AmbiguousEscapedStyleNamesDisableVisualMovement()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="escaped" x="1" y="2" width="20" height="20"
                    style="tr\61 nsform: translate(20px)" />
            </svg>
            """;

        SvgVisualElement escaped = Build(source).Elements.Single();

        Assert.IsFalse(escaped.IsMovable);
        StringAssert.Contains(escaped.UnsupportedReason, "ambiguous");
    }

    [TestMethod]
    public void EntityEncodedStyleNamesAndEscapesCannotBypassPolicy()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="encoded-name" x="1" y="2" width="20" height="20"
                    style="transf&#111;rm: translate(20px)" />
              <rect id="encoded-escape" x="30" y="2" width="20" height="20"
                    style="tr&#92;61 nsform: translate(20px)" />
            </svg>
            """;
        SvgVisualDocument document = Build(source);
        SvgVisualElement encodedName = document.Elements.Single(element =>
            element.SourceElement.Id == "encoded-name");
        SvgVisualElement encodedEscape = document.Elements.Single(element =>
            element.SourceElement.Id == "encoded-escape");

        Assert.IsFalse(encodedName.IsMovable);
        StringAssert.Contains(encodedName.UnsupportedReason, "transformed");
        Assert.IsFalse(encodedEscape.IsMovable);
        StringAssert.Contains(encodedEscape.UnsupportedReason, "ambiguous");
    }

    private SvgVisualDocument Build(string source)
    {
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);
        SvgCanvasSize canvas = new SvgCanvasSizeReader().Read(source);
        return _visualIndexService.Build(index.Document!, canvas);
    }
}
