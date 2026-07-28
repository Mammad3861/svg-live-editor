using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewHtmlBuilderTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private readonly PreviewHtmlBuilder _builder = new();

    [TestMethod]
    public void Build_UsesRestrictiveCspAndDataImage()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"5\" /></svg>";

        string html = _builder.Build(svg, 800, 400, BridgeToken);

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
        string html = _builder.Build(svg, 300, 150, BridgeToken);
        Match cspHash = Regex.Match(html, @"script-src 'sha256-([^']+)'");
        Match script = Regex.Match(html, @"<script>(.*?)</script>", RegexOptions.Singleline);

        Assert.IsTrue(cspHash.Success);
        Assert.IsTrue(script.Success);
        string actualHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(script.Groups[1].Value)));
        Assert.AreEqual(cspHash.Groups[1].Value, actualHash);
        Assert.IsFalse(script.Groups[1].Value.Contains('\r'));
        Assert.AreEqual(1, Regex.Matches(html, "<script>").Count);
        StringAssert.Contains(script.Groups[1].Value, "bridge.postMessage({");
        StringAssert.Contains(script.Groups[1].Value, "event.ctrlKey");
        StringAssert.Contains(script.Groups[1].Value, "event.shiftKey");
        StringAssert.Contains(script.Groups[1].Value, "window.addEventListener(");
        StringAssert.Contains(script.Groups[1].Value, "{ capture: true, passive: false }");
        StringAssert.Contains(script.Groups[1].Value, "event.button === 1");
        StringAssert.Contains(script.Groups[1].Value, "spaceHeld || event.ctrlKey || panModeEnabled");
        StringAssert.Contains(script.Groups[1].Value, "event.target !== image");
        StringAssert.Contains(script.Groups[1].Value, "!event.isTrusted || !event.isPrimary");
        StringAssert.Contains(script.Groups[1].Value, "event.pointerType !== 'mouse'");
        StringAssert.Contains(script.Groups[1].Value, "type: 'directDrag'");
        StringAssert.Contains(script.Groups[1].Value, "type: 'contextMenu'");
        StringAssert.Contains(script.Groups[1].Value, "type: 'copyCommand'");
        StringAssert.Contains(script.Groups[1].Value, "viewport.focus({ preventScroll: true })");
        StringAssert.Contains(script.Groups[1].Value, "type: 'viewport'");
        StringAssert.Contains(script.Groups[1].Value, "token: bridgeToken");
        StringAssert.Contains(script.Groups[1].Value, "message.type === 'zoomState'");
        StringAssert.Contains(script.Groups[1].Value, "Object.keys(message).length === 6");
        StringAssert.Contains(script.Groups[1].Value, "message.token !== bridgeToken");
        StringAssert.Contains(script.Groups[1].Value, "message.renderedWidth <= 10000000");
        StringAssert.Contains(script.Groups[1].Value, "message.centerX <= 1");
        StringAssert.Contains(script.Groups[1].Value, "message.type === 'copyPng'");
        StringAssert.Contains(script.Groups[1].Value, "message.width * message.height <= 8000000");
        StringAssert.Contains(script.Groups[1].Value, "canvas.toDataURL('image/png')");
        StringAssert.Contains(script.Groups[1].Value, "context.drawImage(image");
        StringAssert.Contains(script.Groups[1].Value, "document.body.dataset.hostScriptReady = 'true'");
        Assert.IsFalse(script.Groups[1].Value.Contains(
            "nativeZoomFallback",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_SeparatesZoomWheelFromNormalAndShiftWheel()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string script = ExtractHostScript(
            _builder.Build(svg, 1000, 500, BridgeToken));

        StringAssert.Contains(script, "if (event.ctrlKey)");
        StringAssert.Contains(script, "event.deltaY < 0 ? 'in' : 'out'");
        StringAssert.Contains(script, "if (event.shiftKey)");
        StringAssert.Contains(script, "viewport.scrollLeft += horizontalDelta");
        StringAssert.Contains(script, "postZoomRequest(");
        Assert.IsFalse(script.Contains(
            "viewport.scrollTop += horizontalDelta",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_PlainCopyIsPreviewFocusedAndDoesNotConflictWithOtherGestures()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string html = _builder.Build(svg, 1000, 500, BridgeToken);
        string script = ExtractHostScript(html);

        StringAssert.Contains(html, "tabindex=\"0\"");
        StringAssert.Contains(html, "aria-label=\"Live SVG preview\"");
        StringAssert.Contains(html, ".preview-viewport:focus-visible");
        StringAssert.Contains(script, "event.code === 'KeyC'");
        StringAssert.Contains(script, "event.ctrlKey && !event.shiftKey");
        StringAssert.Contains(script, "!event.altKey && !event.metaKey");
        StringAssert.Contains(script, "postCopyCommand()");
        StringAssert.Contains(script, "if (event.ctrlKey)");
        StringAssert.Contains(script, "spaceHeld || event.ctrlKey || panModeEnabled");
    }

    [TestMethod]
    public void Build_DirectDragRequiresTrustedArtworkGestureAndHostThreshold()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string html = _builder.Build(svg, 1000, 500, BridgeToken);
        string script = ExtractHostScript(html);

        StringAssert.Contains(html, "img.drag-ready");
        StringAssert.Contains(html, "cursor: grab");
        StringAssert.Contains(html, "pointer-events: auto");
        StringAssert.Contains(script, "const choosePointerAction = event =>");
        StringAssert.Contains(script, "event.target !== image");
        StringAssert.Contains(script, "!event.isTrusted || !event.isPrimary");
        StringAssert.Contains(script, "event.pointerType !== 'mouse'");
        StringAssert.Contains(script, "minimumHorizontalDragDistance");
        StringAssert.Contains(script, "minimumVerticalDragDistance");
        StringAssert.Contains(script, "postDirectDragArm(gestureId, event)");
        StringAssert.Contains(script, "postDirectDragSignal('start', gestureId)");
        StringAssert.Contains(script, "postDirectDragSignal('cancel', stopped.gestureId)");
        StringAssert.Contains(script, "stopDirectDrag(event, false)");
        StringAssert.Contains(script, "viewport.addEventListener('dragstart', event => event.preventDefault())");
        Assert.IsFalse(script.Contains(
            "dataTransfer.setData",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_RightClickSuppressesTheBrowserMenuAndSendsOnlyCoordinates()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string script = ExtractHostScript(
            _builder.Build(svg, 1000, 500, BridgeToken));

        StringAssert.Contains(
            script,
            "viewport.addEventListener('contextmenu', event =>");
        StringAssert.Contains(script, "event.preventDefault()");
        StringAssert.Contains(script, "rememberPointer(event)");
        StringAssert.Contains(script, "type: 'contextMenu'");
        StringAssert.Contains(script, "x: lastPointerX");
        StringAssert.Contains(script, "y: lastPointerY");
        StringAssert.Contains(script, "viewportWidth: viewport.clientWidth");
        StringAssert.Contains(script, "viewportHeight: viewport.clientHeight");
        Assert.IsFalse(script.Contains("innerHTML", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("window.open", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_PanStateAcceptsSupportedGesturesAndAlwaysTerminates()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string script = ExtractHostScript(
            _builder.Build(svg, 2000, 1000, BridgeToken));

        StringAssert.Contains(script, "if (event.button === 1)");
        StringAssert.Contains(script, "spaceHeld || event.ctrlKey || panModeEnabled");
        StringAssert.Contains(script, "return 'pan'");
        StringAssert.Contains(script, "if (action !== 'pan' || !canPan())");
        StringAssert.Contains(script, "viewport.setPointerCapture(activePointerId)");
        StringAssert.Contains(script, "activePanButton = event.button");
        StringAssert.Contains(script, "const requiredButtonMask = activePanButton === 1 ? 4 : 1");
        StringAssert.Contains(script, "viewport.addEventListener('pointerup', event =>");
        StringAssert.Contains(script, "viewport.addEventListener('pointercancel', event =>");
        StringAssert.Contains(script, "viewport.addEventListener('lostpointercapture', event =>");
        StringAssert.Contains(script, "viewport.addEventListener('pointerleave', event =>");
        StringAssert.Contains(script, "window.addEventListener('pointerup', event =>");
        StringAssert.Contains(script, "window.addEventListener('blur'");
        StringAssert.Contains(script, "postPanCommand('toggle')");
        StringAssert.Contains(script, "postPanCommand('exit')");
        StringAssert.Contains(script, "event.code === 'KeyH'");
        StringAssert.Contains(script, "event.code === 'Escape'");
        StringAssert.Contains(script, "activePointerId = null");
        StringAssert.Contains(script, "activePanButton = null");
    }

    [TestMethod]
    public void Build_PngRenderingUsesOnlyTheIsolatedImageAndStrictLimits()
    {
        const string svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام</text></svg>";
        string script = ExtractHostScript(
            _builder.Build(svg, 300, 150, BridgeToken));

        StringAssert.Contains(script, "context.drawImage(image, 0, 0");
        StringAssert.Contains(script, "message.width <= 4096");
        StringAssert.Contains(script, "message.height <= 4096");
        StringAssert.Contains(script, "message.width * message.height <= 8000000");
        StringAssert.Contains(script, "mimeType: 'image/png'");
        StringAssert.Contains(script, "payload: dataUrl.slice(prefix.length)");
        Assert.IsFalse(script.Contains("innerHTML", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("eval(", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("fetch(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_CapturesAndRestoresOnlyNormalizedViewportCenters()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string script = ExtractHostScript(
            _builder.Build(svg, 1000, 500, BridgeToken));

        StringAssert.Contains(script, "const rememberPointer = event =>");
        StringAssert.Contains(script, "lastPointerX");
        StringAssert.Contains(script, "lastPointerY");
        StringAssert.Contains(script, "anchorX: safeAnchorX");
        StringAssert.Contains(script, "anchorY: safeAnchorY");
        StringAssert.Contains(script, "const postViewportState = () =>");
        StringAssert.Contains(script, "if (!bridge || !initialViewportApplied)");
        StringAssert.Contains(script, "initialViewportApplied = true");
        StringAssert.Contains(script, "type: 'viewport'");
        StringAssert.Contains(script, "centerX:");
        StringAssert.Contains(script, "centerY:");
        StringAssert.Contains(script, "viewport.addEventListener('scroll', scheduleViewportState)");
        StringAssert.Contains(script, "image.addEventListener('load', initializeViewport");
        StringAssert.Contains(script, "requestAnimationFrame(() => requestAnimationFrame(applyInitialViewport))");
        StringAssert.Contains(script, "image.style.width = `${message.renderedWidth}px`");
        StringAssert.Contains(script, "stage.style.width = `${message.renderedWidth + 48}px`");
        StringAssert.Contains(script, "restoreViewportCenter(message.centerX, message.centerY)");
    }

    [TestMethod]
    public void Build_EmbedsOnlyClampedNormalizedViewportCoordinates()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
        string html = _builder.Build(
            svg,
            300,
            150,
            BridgeToken,
            new PreviewViewportPosition(1.5, double.NaN));

        StringAssert.Contains(html, "data-initial-center-x=\"1\"");
        StringAssert.Contains(html, "data-initial-center-y=\"0.5\"");
    }

    [TestMethod]
    public void Build_DoesNotAllowMarkupToEscapeThePreviewContainer()
    {
        const string attackerControlledText = "</img><script>window.open('https://example.test')</script><iframe srcdoc=\"bad\"></iframe>";

        string html = _builder.Build(attackerControlledText, 300, 150, BridgeToken);

        Assert.IsFalse(html.Contains("<script>window.open", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<iframe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("srcdoc=", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, Convert.ToBase64String(Encoding.UTF8.GetBytes(attackerControlledText)));
    }

    [TestMethod]
    public void Build_ChangesOnlyTheSvgImageDimensions()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        string smaller = _builder.Build(svg, 200, 100, BridgeToken);
        string larger = _builder.Build(svg, 500, 250, BridgeToken);

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

        string html = _builder.Build(svg, 300, 150, BridgeToken);

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

        string html = _builder.Build(svg, 300, 150, BridgeToken);
        int encodedStart = html.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int encodedEnd = html.IndexOf('"', encodedStart);
        string encodedSvg = html[encodedStart..encodedEnd];
        string decodedSvg = Encoding.UTF8.GetString(Convert.FromBase64String(encodedSvg));

        Assert.AreEqual(svg, decodedSvg);
    }

    [TestMethod]
    public void Build_RejectsAnInvalidBridgeToken()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";

        Assert.Throws<ArgumentException>(
            () => _builder.Build(svg, 300, 150, "not-a-token"));
    }

    private static string ExtractHostScript(string html)
    {
        Match match = Regex.Match(
            html,
            @"<script>(.*?)</script>",
            RegexOptions.Singleline);
        Assert.IsTrue(match.Success);
        return match.Groups[1].Value;
    }
}
