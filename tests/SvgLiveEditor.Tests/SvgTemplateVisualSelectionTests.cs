using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTemplateVisualSelectionTests
{
    private sealed record ExpectedElementState(
        string ElementName,
        int Count,
        bool Selectable,
        bool Movable,
        string? ReasonFragment = null);

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

    private static readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<ExpectedElementState>> ExpectedMatrix =
        new Dictionary<string, IReadOnlyList<ExpectedElementState>>(
            StringComparer.Ordinal)
        {
            ["blank"] = [],
            ["app-icon"] =
            [
                new("rect", 1, true, true),
                new("path", 1, true, false, "this version")
            ],
            ["social-card"] =
            [
                new("rect", 2, true, true),
                new("circle", 2, true, true),
                new("text", 3, true, true)
            ],
            ["flow-diagram"] =
            [
                new("rect", 4, true, true),
                new("path", 1, false, false, "marker-decorated"),
                new("text", 3, true, true)
            ],
            ["persian-rtl"] =
            [
                new("rect", 2, true, true),
                new("text", 8, true, true),
                new("circle", 1, true, true),
                new("path", 1, true, false, "this version")
            ]
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
    public void EveryBuiltInTemplateMatchesTheSelectionAndMovementMatrix()
    {
        foreach (SvgTemplateDefinition template in
                 new SvgTemplateCatalog().LoadAll())
        {
            SvgVisualDocument visual = BuildWithTrustedTextBounds(
                template.Source);
            IReadOnlyList<ExpectedElementState> expected =
                ExpectedMatrix[template.Id];
            Assert.AreEqual(
                expected.Sum(row => row.Count),
                visual.Elements.Count,
                $"{template.Id} total visible direct elements");

            foreach (ExpectedElementState row in expected)
            {
                SvgVisualElement[] matches = visual.Elements
                    .Where(element => element.SourceElement.Name.Equals(
                        row.ElementName,
                        StringComparison.Ordinal))
                    .ToArray();
                Assert.HasCount(
                    row.Count,
                    matches,
                    $"{template.Id} {row.ElementName} count");
                Assert.IsTrue(matches.All(element =>
                    element.IsSelectable == row.Selectable),
                    $"{template.Id} {row.ElementName} selectable state");
                Assert.IsTrue(matches.All(element =>
                    element.IsMovable == row.Movable),
                    $"{template.Id} {row.ElementName} movable state");
                if (row.ReasonFragment is not null)
                {
                    Assert.IsTrue(matches.All(element =>
                        element.UnsupportedReason?.Contains(
                            row.ReasonFragment,
                            StringComparison.Ordinal) == true));
                }
                else if (row.Movable)
                {
                    Assert.IsTrue(matches.All(element =>
                        string.IsNullOrWhiteSpace(
                            element.UnsupportedReason)));
                }
            }
        }
    }

    [TestMethod]
    public void SimpleUnsupportedPathIsInspectableOnlyWithinItsLocalBounds()
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
        Assert.AreEqual(
            "path",
            pathCoveredPoint.Element?.SourceElement.Name);
        Assert.IsTrue(pathCoveredPoint.Element?.IsSelectable);
        Assert.IsFalse(pathCoveredPoint.Element?.IsMovable);
        Assert.IsNull(pathCoveredPoint.Blocker);
        StringAssert.Contains(
            pathCoveredPoint.Element?.UnsupportedReason,
            "this version");
    }

    [TestMethod]
    public void UnsupportedPathWithoutReliableBoundsRemainsFailClosed()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect id="under" x="5" y="5" width="20" height="20" />
              <path id="curve" d="M70 70 C80 10 90 90 95 20" />
            </svg>
            """;

        SvgVisualHitTestResult result =
            new SvgVisualHitTestService().HitTestDetailed(
                Build(source),
                new SvgMappedPreviewPoint(
                    new SvgVisualPoint(10, 10),
                    1));

        Assert.IsNull(result.Element);
        Assert.AreEqual("curve", result.Blocker?.SourceElement.Id);
        Assert.IsFalse(result.Blocker?.IsSelectable);
        StringAssert.Contains(
            result.Blocker?.UnsupportedReason,
            "reliable conservative bounds");
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

    private static SvgVisualDocument BuildWithTrustedTextBounds(string source)
    {
        SvgVisualDocument pending = Build(source);
        SvgVisualTextMeasurementResult[] measurements = pending.Elements
            .Where(element => element.TextMeasurement is not null)
            .Select(element => element.TextMeasurement!)
            .Select(measurement => new SvgVisualTextMeasurementResult(
                measurement.Index,
                true,
                new SvgVisualBounds(
                    measurement.X,
                    measurement.Y - measurement.FontSize,
                    measurement.X + Math.Max(
                        measurement.FontSize,
                        measurement.Text.Length * measurement.FontSize * 0.5),
                    measurement.Y)))
            .ToArray();
        return new SvgVisualTextMeasurementService().Apply(
            pending,
            measurements);
    }
}
