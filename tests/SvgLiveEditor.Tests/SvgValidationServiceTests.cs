using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgValidationServiceTests
{
    private readonly SvgValidationService _service = new();

    [TestMethod]
    public void Validate_AcceptsSafeSvg()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <defs><linearGradient id="g"><stop offset="0" stop-color="#fff" /></linearGradient></defs>
              <rect width="100" height="100" fill="url(#g)" />
              <text x="10" y="50">سلام SVG</text>
            </svg>
            """;

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsTrue(result.IsValid, result.Message);
    }

    [TestMethod]
    public void Validate_AcceptsPassiveBidiPresentationWithoutAddingActiveContent()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text direction="rtl" unicode-bidi="embed" text-anchor="start">سلام! من بهروز هستم.</text>
              <text direction="ltr" unicode-bidi="plaintext" text-anchor="middle">Hello — سلام!</text>
            </svg>
            """;

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsTrue(result.IsValid, result.Message);
        Assert.IsFalse(svg.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(svg.Contains(" on", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(svg.Contains("href=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Validate_AcceptsSupportedSafeSvgFeatures()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 140">
              <defs>
                <linearGradient id="gradient"><stop offset="0" stop-color="#fff" /><stop offset="1" stop-color="#06c" /></linearGradient>
                <marker id="arrow" markerWidth="8" markerHeight="8" refX="7" refY="4"><path d="M0 0 L8 4 L0 8 Z" /></marker>
                <clipPath id="clip"><rect width="220" height="120" rx="8" /></clipPath>
                <mask id="mask"><rect width="240" height="140" fill="#fff" /></mask>
                <filter id="soft"><feGaussianBlur stdDeviation="0.5" /></filter>
              </defs>
              <g clip-path="url(#clip)" mask="url(#mask)" filter="url(#soft)">
                <rect width="240" height="140" fill="url(#gradient)" style="stroke:#123;stroke-width:2" />
                <path d="M20 80 L190 80" marker-end="url(#arrow)" />
                <text x="20" y="45">سلام SVG</text>
              </g>
            </svg>
            """;

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsTrue(result.IsValid, result.Message);
    }

    [TestMethod]
    public void Validate_RejectsMalformedXmlWithLocation()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect>
            </svg>
            """;

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        Assert.IsNotNull(result.LineNumber);
        Assert.IsNotNull(result.ColumnNumber);
    }

    [TestMethod]
    public void Validate_RejectsMissingSvgRoot()
    {
        Models.SvgValidationResult result = _service.Validate("<root />");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "root");
    }

    [TestMethod]
    public void Validate_RejectsIncorrectSvgNamespace()
    {
        Models.SvgValidationResult result = _service.Validate("<svg xmlns=\"https://example.test/not-svg\" />");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, SvgValidationService.SvgNamespace);
    }

    [TestMethod]
    public void Validate_RejectsDtd()
    {
        const string svg = "<!DOCTYPE svg [<!ELEMENT svg ANY>]><svg xmlns=\"http://www.w3.org/2000/svg\" />";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "DTD");
    }

    [TestMethod]
    public void Validate_RejectsExternalEntityWithoutResolvingIt()
    {
        const string svg = "<!DOCTYPE svg [<!ENTITY secret SYSTEM \"file:///C:/sensitive.txt\">]><svg xmlns=\"http://www.w3.org/2000/svg\">&secret;</svg>";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "unsafe");
    }

    [TestMethod]
    public void Validate_RejectsScripts()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "script");
    }

    [TestMethod]
    public void Validate_RejectsInlineEventHandlers()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" onclick=\"alert(1)\" /></svg>";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "event handler");
    }

    [TestMethod]
    [DataRow("<image href=\"https://example.test/pixel.png\" />")]
    [DataRow("<use href=\"file:///C:/secret.svg#item\" />")]
    [DataRow("<rect style=\"fill:url(https://example.test/paint.svg)\" />")]
    public void Validate_RejectsExternalResources(string child)
    {
        string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\">{child}</svg>";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Message.Contains("external", StringComparison.OrdinalIgnoreCase), result.Message);
    }

    [TestMethod]
    public void Validate_RejectsForeignObject()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><foreignObject /></svg>";

        Models.SvgValidationResult result = _service.Validate(svg);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "foreignObject");
    }
}
