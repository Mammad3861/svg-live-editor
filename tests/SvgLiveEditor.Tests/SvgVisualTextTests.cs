using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualTextTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgVisualGeometryIndexService _visualIndexService = new();
    private readonly SvgVisualTextMeasurementService _measurementService = new();

    [TestMethod]
    public void SimpleEnglishTextProducesStrictMeasurementInput()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="-20 10 400 200">
              <text id="title" x="40px" y="80"
                    font-family="Segoe UI, sans-serif"
                    font-size="24px" font-weight="700"
                    font-style="italic">Hello SVG</text>
            </svg>
            """;

        SvgVisualElement text = Build(source).Elements.Single();
        SvgVisualTextMeasurementSpec measurement = text.TextMeasurement!;

        Assert.AreEqual(SvgVisualElementKind.Text, text.Kind);
        Assert.IsFalse(text.IsMovable);
        StringAssert.Contains(text.UnsupportedReason, "waiting");
        Assert.AreEqual("Hello SVG", measurement.Text);
        Assert.AreEqual(40, measurement.X);
        Assert.AreEqual(80, measurement.Y);
        Assert.AreEqual(24, measurement.FontSize);
        Assert.AreEqual("Segoe UI, sans-serif", measurement.FontFamily);
        Assert.AreEqual("700", measurement.FontWeight);
        Assert.AreEqual("italic", measurement.FontStyle);
        Assert.AreEqual(-20, Build(source).Viewport.MinX);
    }

    [TestMethod]
    public void PersianTextPreservesExactTextAndBidiInputs()
    {
        const string persian = "سلام SVG، نسخه ۶.";
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="fa" x="300" y="70" font-size="28"
                    font-family="Tahoma, sans-serif"
                    text-anchor="end" direction="rtl"
                    unicode-bidi="plaintext">سلام SVG، نسخه ۶.</text>
            </svg>
            """;

        SvgVisualTextMeasurementSpec measurement =
            Build(source).Elements.Single().TextMeasurement!;

        Assert.AreEqual(persian, measurement.Text);
        Assert.AreEqual("end", measurement.TextAnchor);
        Assert.AreEqual("rtl", measurement.Direction);
        Assert.AreEqual("plaintext", measurement.UnicodeBidi);
        CollectionAssert.AreEqual(
            System.Text.Encoding.UTF8.GetBytes(persian),
            System.Text.Encoding.UTF8.GetBytes(measurement.Text));
    }

    [TestMethod]
    [DataRow("start", "ltr")]
    [DataRow("middle", "ltr")]
    [DataRow("end", "rtl")]
    public void AnchorAndDirectionAreCarriedWithoutNormalization(
        string anchor,
        string direction)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"10\" y=\"20\" font-size=\"16\" text-anchor=\"{anchor}\" direction=\"{direction}\">Text</text></svg>";

        SvgVisualTextMeasurementSpec measurement =
            Build(source).Elements.Single().TextMeasurement!;

        Assert.AreEqual(anchor, measurement.TextAnchor);
        Assert.AreEqual(direction, measurement.Direction);
    }

    [TestMethod]
    public void EntityEncodedTypographyMatchesBrowserXmlDecoding()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text x="10" y="20"
                    style="font-s&#105;ze: 24px; font-family: Tahoma, sans-serif">سلام</text>
            </svg>
            """;

        SvgVisualTextMeasurementSpec measurement =
            Build(source).Elements.Single().TextMeasurement!;

        Assert.AreEqual(24, measurement.FontSize);
        Assert.AreEqual("Tahoma, sans-serif", measurement.FontFamily);
        Assert.AreEqual("سلام", measurement.Text);
    }

    [TestMethod]
    public void MeasuredTextIsTopmostOverBackgroundAndCanBeSelected()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
              <rect id="background" x="0" y="0" width="200" height="100" />
              <text id="label" x="20" y="50" font-size="24">Top text</text>
            </svg>
            """;
        SvgVisualDocument pending = Build(source);
        SvgVisualDocument measured = _measurementService.Apply(
            pending,
            [new SvgVisualTextMeasurementResult(
                0,
                true,
                new SvgVisualBounds(20, 30, 110, 54))]);

        SvgVisualElement? hit = new SvgVisualHitTestService().HitTest(
            measured,
            new SvgMappedPreviewPoint(
                new SvgVisualPoint(40, 45),
                1));

        Assert.AreEqual("label", hit?.SourceElement.Id);
        Assert.IsTrue(hit?.IsMovable);
    }

    [TestMethod]
    [DataRow("<tspan>nested</tspan>", "tspan")]
    [DataRow("<textPath href=\"#p\">path text</textPath>", "textPath")]
    public void NestedTextLayoutsAreNotVisuallyMovable(
        string content,
        string expectedReason)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><path id=\"p\" d=\"M0 0L100 0\"/><text id=\"label\" x=\"1\" y=\"2\" font-size=\"12\">{content}</text></svg>";
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);

        SvgVisualElement text = _visualIndexService.Build(
            index.Document!,
            new SvgCanvasSizeReader().Read(source),
            source).Elements.Single(element =>
                element.SourceElement.Id == "label");

        Assert.IsFalse(text.IsMovable);
        Assert.IsNull(text.TextMeasurement);
        StringAssert.Contains(text.UnsupportedReason, expectedReason);
    }

    [TestMethod]
    [DataRow("x=\"10 20\" y=\"30\" font-size=\"16\"", "x and y")]
    [DataRow("x=\"10%\" y=\"30\" font-size=\"16\"", "x and y")]
    [DataRow("x=\"10\" y=\"30\" font-size=\"1em\"", "font-size")]
    [DataRow("x=\"10\" y=\"30\" font-size=\"0\"", "font-size")]
    [DataRow("x=\"10\" y=\"30\" dx=\"1\" font-size=\"16\"", "complex text layout")]
    [DataRow("x=\"10\" y=\"30\" rotate=\"5\" font-size=\"16\"", "complex text layout")]
    [DataRow("x=\"10\" y=\"30\" font-size=\"16\" transform=\"translate(1)\"", "transformed")]
    [DataRow("x=\"10\" y=\"30\" font-size=\"16\" filter=\"url(#f)\"", "filtered")]
    public void UnsupportedGeometryAndEffectsStayOutOfHitTesting(
        string attributes,
        string expectedReason)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" {attributes}>Text</text></svg>";
        SvgVisualElement text = Build(source).Elements.Single();

        Assert.IsFalse(text.IsMovable);
        Assert.IsNull(text.TextMeasurement);
        StringAssert.Contains(text.UnsupportedReason, expectedReason);
    }

    [TestMethod]
    public void MeasurementFailureRemainsConservativelyUnsupported()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"1\" y=\"20\" font-size=\"16\">Text</text></svg>";
        SvgVisualDocument measured = _measurementService.Apply(
            Build(source),
            [new SvgVisualTextMeasurementResult(0, false, null)]);

        SvgVisualElement text = measured.Elements.Single();
        Assert.IsFalse(text.IsMovable);
        StringAssert.Contains(text.UnsupportedReason, "reliable bounds");
    }

    [TestMethod]
    public void XmlLegalControlTextIsRejectedBeforeMessageConstruction()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"1\" y=\"20\" font-size=\"16\">A\u0085B</text></svg>";

        SvgVisualElement text = Build(source).Elements.Single();

        Assert.IsFalse(text.IsMovable);
        Assert.IsNull(text.TextMeasurement);
        StringAssert.Contains(text.UnsupportedReason, "control");
    }

    private SvgVisualDocument Build(string source)
    {
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);
        return _visualIndexService.Build(
            index.Document!,
            new SvgCanvasSizeReader().Read(source),
            source);
    }
}
