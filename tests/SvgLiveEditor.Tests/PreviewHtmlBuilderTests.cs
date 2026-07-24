using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewHtmlBuilderTests
{
    private readonly PreviewHtmlBuilder _builder = new();

    [TestMethod]
    public void Build_UsesRestrictiveCspAndDataImage()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"5\" /></svg>";

        string html = _builder.Build(svg, renderedWidth: 800, renderedHeight: 400);

        StringAssert.Contains(html, "default-src 'none'");
        StringAssert.Contains(html, "script-src 'sha256-");
        StringAssert.Contains(html, "connect-src 'none'");
        StringAssert.Contains(html, "img-src data:");
        StringAssert.Contains(html, "data:image/svg+xml;base64,");
        StringAssert.Contains(html, Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
        Assert.IsFalse(html.Contains(svg, StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("eval(", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<script src=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Build_AllowsOnlyTheExactStaticHostScriptByHash()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string html = _builder.Build(svg, renderedWidth: 300, renderedHeight: 150);
        Match cspHash = Regex.Match(html, @"script-src 'sha256-([^']+)'");
        Match script = Regex.Match(html, @"<script>(.*?)</script>", RegexOptions.Singleline);

        Assert.IsTrue(cspHash.Success);
        Assert.IsTrue(script.Success);
        string actualHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(script.Groups[1].Value)));
        Assert.AreEqual(cspHash.Groups[1].Value, actualHash);
        Assert.AreEqual(1, Regex.Matches(html, "<script>").Count);
        StringAssert.Contains(script.Groups[1].Value, "bridge.postMessage({");
        StringAssert.Contains(script.Groups[1].Value, "event.ctrlKey");
        StringAssert.Contains(script.Groups[1].Value, "event.shiftKey");
        StringAssert.Contains(script.Groups[1].Value, "event.button === 1");
        StringAssert.Contains(script.Groups[1].Value, "event.button === 0 && spaceHeld");
    }

    [TestMethod]
    public void Build_EmbedsOnlySanitizedInMemoryScrollCoordinates()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string html = _builder.Build(
            svg,
            renderedWidth: 300,
            renderedHeight: 150,
            new PreviewScrollPosition(123.5, double.NaN));

        StringAssert.Contains(html, "data-initial-scroll-left=\"123.5\"");
        StringAssert.Contains(html, "data-initial-scroll-top=\"0\"");
    }

    [TestMethod]
    public void Build_DoesNotAllowMarkupToEscapeThePreviewContainer()
    {
        const string attackerControlledText = "</img><script>window.open('https://example.test')</script><iframe srcdoc=\"bad\"></iframe>";

        string html = _builder.Build(attackerControlledText, renderedWidth: 300, renderedHeight: 150);

        Assert.IsFalse(html.Contains("<script>window.open", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<iframe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("srcdoc=", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, Convert.ToBase64String(Encoding.UTF8.GetBytes(attackerControlledText)));
    }

    [TestMethod]
    public void Build_ChangesOnlyTheSvgImageDimensions()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string smaller = _builder.Build(svg, renderedWidth: 200, renderedHeight: 100);
        string larger = _builder.Build(svg, renderedWidth: 500, renderedHeight: 250);

        StringAssert.Contains(smaller, "width: 200px;");
        StringAssert.Contains(smaller, "height: 100px;");
        StringAssert.Contains(larger, "width: 500px;");
        StringAssert.Contains(larger, "height: 250px;");
        StringAssert.Contains(smaller, "background-size: 24px 24px");
        StringAssert.Contains(larger, "background-size: 24px 24px");
        Assert.IsFalse(smaller.Contains("object-fit", StringComparison.Ordinal));
        Assert.IsFalse(larger.Contains("zoom:", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Build_UsesVisibleLightCheckerboardCanvas()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string html = _builder.Build(svg, renderedWidth: 300, renderedHeight: 150);

        StringAssert.Contains(html, "<meta name=\"color-scheme\" content=\"light\">");
        StringAssert.Contains(html, "color-scheme: only light");
        StringAssert.Contains(html, "background-color: #f8fafc");
        StringAssert.Contains(html, "background-image:");
    }

    [TestMethod]
    public void Build_Base64ImageRoundTripsPersianText()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام SVG</text></svg>";
        const string prefix = "src=\"data:image/svg+xml;base64,";

        string html = _builder.Build(svg, renderedWidth: 300, renderedHeight: 150);
        int encodedStart = html.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int encodedEnd = html.IndexOf('"', encodedStart);
        string encodedSvg = html[encodedStart..encodedEnd];
        string decodedSvg = Encoding.UTF8.GetString(Convert.FromBase64String(encodedSvg));

        Assert.AreEqual(svg, decodedSvg);
    }
}
