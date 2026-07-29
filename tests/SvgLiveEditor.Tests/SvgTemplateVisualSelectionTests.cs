using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTemplateVisualSelectionTests
{
    private static readonly IReadOnlyDictionary<
        string,
        (int Supported, int Unsupported)> ExpectedInventory =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["blank"] = (0, 0),
            ["app-icon"] = (1, 1),
            ["social-card"] = (7, 0),
            ["flow-diagram"] = (7, 1),
            ["persian-rtl"] = (11, 1)
        };

    [TestMethod]
    public void EveryBuiltInTemplateHasTheDocumentedVisualSupportInventory()
    {
        foreach (SvgTemplateDefinition template in
                 new SvgTemplateCatalog().LoadAll())
        {
            SvgVisualDocument visual = Build(template.Source);
            SvgVisualElement[] supported = visual.Elements
                .Where(element =>
                    element.Kind != SvgVisualElementKind.Unsupported)
                .ToArray();
            SvgVisualElement[] unsupported = visual.Elements
                .Where(element =>
                    element.Kind == SvgVisualElementKind.Unsupported)
                .ToArray();
            (int expectedSupported, int expectedUnsupported) =
                ExpectedInventory[template.Id];

            Assert.HasCount(
                expectedSupported,
                supported,
                $"{template.Id} supported inventory");
            Assert.HasCount(
                expectedUnsupported,
                unsupported,
                $"{template.Id} unsupported inventory");
            Assert.IsTrue(
                supported.All(element =>
                    element.Kind == SvgVisualElementKind.Text
                        ? element.TextMeasurement is not null
                        : element.IsMovable),
                $"{template.Id} contains a documented shape/text element that cannot enter its normal selectable state.");
            Assert.IsTrue(unsupported.All(element =>
                element.SourceElement.Name is
                    "path" or "polygon" or "polyline"));
        }
    }

    [TestMethod]
    public void SimpleUnsupportedPathBlocksOnlyItsConservativeLocalBounds()
    {
        SvgTemplateDefinition template = new SvgTemplateCatalog()
            .LoadAll()
            .Single(item => item.Id == "persian-rtl");
        SvgVisualDocument visual = Build(template.Source);
        SvgVisualHitTestService hitTest = new();

        SvgVisualHitTestResult clearCirclePoint =
            hitTest.HitTestDetailed(
                visual,
                new SvgMappedPreviewPoint(
                    new SvgVisualPoint(155, 297),
                    1));
        SvgVisualHitTestResult pathCoveredPoint =
            hitTest.HitTestDetailed(
                visual,
                new SvgMappedPreviewPoint(
                    new SvgVisualPoint(155, 335),
                    1));

        Assert.AreEqual(
            "circle",
            clearCirclePoint.Element?.SourceElement.Name);
        Assert.IsNull(clearCirclePoint.Blocker);
        Assert.IsNull(pathCoveredPoint.Element);
        Assert.AreEqual(
            "path",
            pathCoveredPoint.Blocker?.SourceElement.Name);
        StringAssert.Contains(
            pathCoveredPoint.Blocker?.UnsupportedReason,
            "v0.6.0");
    }

    [TestMethod]
    public void DefinitionContentIsNotIndexedAsDirectVisibleArtwork()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <defs>
                <rect id="definition" width="100" height="100" />
                <path id="marker-path" d="M0 0L10 5L0 10Z" />
              </defs>
              <filter id="paint-filter">
                <rect id="filter-input" width="100" height="100" />
              </filter>
              <rect id="visible" x="10" y="10" width="20" height="20" />
            </svg>
            """;
        SvgVisualDocument visual = Build(source);

        Assert.HasCount(1, visual.Elements);
        Assert.AreEqual(
            "visible",
            visual.Elements.Single().SourceElement.Id);
    }

    [TestMethod]
    public void OffPointerReliableBlockerDoesNotSuppressSupportedHit()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="target" x="5" y="5" width="20" height="20" />
              <path id="elsewhere" d="M70 70H90V90H70Z" />
            </svg>
            """;
        SvgVisualHitTestResult result =
            new SvgVisualHitTestService().HitTestDetailed(
                Build(source),
                new SvgMappedPreviewPoint(
                    new SvgVisualPoint(10, 10),
                    1));

        Assert.AreEqual("target", result.Element?.SourceElement.Id);
        Assert.IsNull(result.Blocker);
    }

    [TestMethod]
    public void AnimatedSupportedShapeRemainsConservativelyBlocked()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="animated" x="5" y="5" width="20" height="20">
                <animate attributeName="x" from="5" to="50" dur="1s" />
              </rect>
            </svg>
            """;
        SvgVisualElement rectangle = Build(source).Elements.Single();

        Assert.IsFalse(rectangle.IsMovable);
        Assert.IsNull(rectangle.Geometry);
        StringAssert.Contains(
            rectangle.UnsupportedReason,
            "animated");
    }

    private static SvgVisualDocument Build(string source)
    {
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        return new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
    }
}
