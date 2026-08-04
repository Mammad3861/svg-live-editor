using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewInteractionTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private readonly PreviewInteractionMessageParser _parser = new();
    private readonly PreviewScrollCalculator _scrollCalculator = new();

    [TestMethod]
    public void ZoomMessage_ParsesOnlyTheNarrowTrustedShape()
    {
        const string json = """
            {
              "type":"zoom",
              "token":"00112233445566778899AABBCCDDEEFF",
              "direction":"in",
              "contentX":0.5,
              "contentY":0.25,
              "anchorX":300,
              "anchorY":200,
              "viewportWidth":600,
              "viewportHeight":400
            }
            """;

        Assert.IsTrue(_parser.TryParseZoomRequest(
            json,
            BridgeToken,
            out PreviewZoomRequest request));
        Assert.AreEqual(PreviewZoomDirection.In, request.Direction);
        Assert.AreEqual(0.5, request.ContentX, 0.0001);
        Assert.AreEqual(300, request.AnchorX, 0.0001);
    }

    [TestMethod]
    public void ZoomMessage_RejectsUnknownCommandsAndExtraProperties()
    {
        const string unknown = """
            {"type":"navigate","token":"00112233445566778899AABBCCDDEEFF",
             "direction":"in","contentX":0.5,"contentY":0.5,
             "anchorX":1,"anchorY":1,"viewportWidth":10,"viewportHeight":10}
            """;
        const string extra = """
            {"type":"zoom","token":"00112233445566778899AABBCCDDEEFF",
             "direction":"out","contentX":0.5,"contentY":0.5,
             "anchorX":1,"anchorY":1,"viewportWidth":10,"viewportHeight":10,
             "url":"https://example.test"}
            """;
        Assert.IsFalse(_parser.TryParseZoomRequest(unknown, BridgeToken, out _));
        Assert.IsFalse(_parser.TryParseZoomRequest(extra, BridgeToken, out _));
    }

    [TestMethod]
    public void ZoomMessage_RejectsAStaleNavigationToken()
    {
        const string json = """
            {"type":"zoom","token":"FFEEDDCCBBAA99887766554433221100",
             "direction":"in","contentX":0.5,"contentY":0.5,
             "anchorX":1,"anchorY":1,"viewportWidth":10,"viewportHeight":10}
            """;

        Assert.IsFalse(_parser.TryParseZoomRequest(json, BridgeToken, out _));
    }

    [TestMethod]
    public void ViewportMessage_RequiresTheExactTokenBoundFiniteShape()
    {
        const string valid = """
            {"type":"viewport","token":"00112233445566778899AABBCCDDEEFF",
             "centerX":0.75,"centerY":0.25}
            """;
        const string stale = """
            {"type":"viewport","token":"FFEEDDCCBBAA99887766554433221100",
             "centerX":0.75,"centerY":0.25}
            """;
        const string outOfRange = """
            {"type":"viewport","token":"00112233445566778899AABBCCDDEEFF",
             "centerX":1.1,"centerY":0.25}
            """;
        const string extra = """
            {"type":"viewport","token":"00112233445566778899AABBCCDDEEFF",
             "centerX":0.75,"centerY":0.25,"selector":"body"}
            """;

        Assert.IsTrue(_parser.TryParseViewportPosition(
            valid,
            BridgeToken,
            out PreviewViewportPosition viewport));
        Assert.AreEqual(0.75, viewport.CenterX, 0.0001);
        Assert.AreEqual(0.25, viewport.CenterY, 0.0001);
        Assert.IsFalse(_parser.TryParseViewportPosition(stale, BridgeToken, out _));
        Assert.IsFalse(_parser.TryParseViewportPosition(outOfRange, BridgeToken, out _));
        Assert.IsFalse(_parser.TryParseViewportPosition(extra, BridgeToken, out _));
    }

    [TestMethod]
    public void PanCommand_RequiresAnExactTokenBoundToggleOrExitSchema()
    {
        const string toggle = """
            {"type":"panCommand","token":"00112233445566778899AABBCCDDEEFF",
             "command":"toggle"}
            """;
        const string exit = """
            {"type":"panCommand","token":"00112233445566778899AABBCCDDEEFF",
             "command":"exit"}
            """;
        const string stale = """
            {"type":"panCommand","token":"FFEEDDCCBBAA99887766554433221100",
             "command":"toggle"}
            """;
        const string extra = """
            {"type":"panCommand","token":"00112233445566778899AABBCCDDEEFF",
             "command":"toggle","script":"alert(1)"}
            """;

        Assert.IsTrue(_parser.TryParsePanCommand(
            toggle,
            BridgeToken,
            out PreviewPanCommand toggleCommand));
        Assert.AreEqual(PreviewPanCommand.Toggle, toggleCommand);
        Assert.IsTrue(_parser.TryParsePanCommand(
            exit,
            BridgeToken,
            out PreviewPanCommand exitCommand));
        Assert.AreEqual(PreviewPanCommand.Exit, exitCommand);
        Assert.IsFalse(_parser.TryParsePanCommand(
            stale,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParsePanCommand(
            extra,
            BridgeToken,
            out _));
    }

    [TestMethod]
    public void ContextMenuRequest_RequiresExactTokenBoundCoordinates()
    {
        const string valid = """
            {"type":"contextMenu",
             "token":"00112233445566778899AABBCCDDEEFF",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":7,"selectionId":"AABBCCDDEEFF00112233445566778899"}
            """;
        const string stale = """
            {"type":"contextMenu",
             "token":"FFEEDDCCBBAA99887766554433221100",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":7,"selectionId":"AABBCCDDEEFF00112233445566778899"}
            """;
        const string outside = """
            {"type":"contextMenu",
             "token":"00112233445566778899AABBCCDDEEFF",
             "x":501,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":7,"selectionId":"AABBCCDDEEFF00112233445566778899"}
            """;
        const string extra = """
            {"type":"contextMenu",
             "token":"00112233445566778899AABBCCDDEEFF",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":7,"selectionId":"AABBCCDDEEFF00112233445566778899",
             "url":"https://example.test"}
            """;
        const string invalidSelection = """
            {"type":"contextMenu",
             "token":"00112233445566778899AABBCCDDEEFF",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":7,"selectionId":"not-an-opaque-id"}
            """;
        const string invalidRevision = """
            {"type":"contextMenu",
             "token":"00112233445566778899AABBCCDDEEFF",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "sourceRevision":-1,"selectionId":""}
            """;

        Assert.IsTrue(_parser.TryParseContextMenuRequest(
            valid,
            BridgeToken,
            out PreviewContextMenuRequest request));
        Assert.AreEqual(250, request.X);
        Assert.AreEqual(125, request.Y);
        Assert.AreEqual(7, request.SourceRevision);
        Assert.AreEqual(
            "AABBCCDDEEFF00112233445566778899",
            request.SelectionId);
        Assert.IsFalse(_parser.TryParseContextMenuRequest(
            stale,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParseContextMenuRequest(
            outside,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParseContextMenuRequest(
            extra,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParseContextMenuRequest(
            invalidSelection,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParseContextMenuRequest(
            invalidRevision,
            BridgeToken,
            out _));
    }

    [TestMethod]
    public void CopyCommand_RequiresOnlyTheExactTypeAndNavigationToken()
    {
        const string valid = """
            {"type":"copyCommand",
             "token":"00112233445566778899AABBCCDDEEFF"}
            """;
        const string stale = """
            {"type":"copyCommand",
             "token":"FFEEDDCCBBAA99887766554433221100"}
            """;
        const string extra = """
            {"type":"copyCommand",
             "token":"00112233445566778899AABBCCDDEEFF",
             "target":"document"}
            """;

        Assert.IsTrue(_parser.IsCopyCommand(valid, BridgeToken));
        Assert.IsFalse(_parser.IsCopyCommand(stale, BridgeToken));
        Assert.IsFalse(_parser.IsCopyCommand(extra, BridgeToken));
        Assert.IsFalse(_parser.IsCopyCommand(
            """{"type":"navigate","token":"00112233445566778899AABBCCDDEEFF"}""",
            BridgeToken));
    }

    [TestMethod]
    public void DirectDragArm_RequiresExactCurrentTokenAndBoundedArtworkSchema()
    {
        const string valid = """
            {"type":"directDrag",
             "token":"00112233445566778899AABBCCDDEEFF",
             "action":"arm",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "button":0,"startedOnArtwork":true,
             "isPrimary":true,"pointerType":"mouse",
             "ctrlKey":false,"shiftKey":false,"altKey":false,
             "metaKey":false,"spaceHeld":false}
            """;
        const string stale = """
            {"type":"directDrag",
             "token":"FFEEDDCCBBAA99887766554433221100",
             "action":"arm",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "button":0,"startedOnArtwork":true,
             "isPrimary":true,"pointerType":"mouse",
             "ctrlKey":false,"shiftKey":false,"altKey":false,
             "metaKey":false,"spaceHeld":false}
            """;
        const string extra = """
            {"type":"directDrag",
             "token":"00112233445566778899AABBCCDDEEFF",
             "action":"arm",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250,
             "button":0,"startedOnArtwork":true,
             "isPrimary":true,"pointerType":"mouse",
             "ctrlKey":false,"shiftKey":false,"altKey":false,
             "metaKey":false,"spaceHeld":false,
             "path":"C:\\secret.png"}
            """;

        Assert.IsTrue(_parser.TryParseDirectDragArmRequest(
            valid,
            BridgeToken,
            out PreviewDirectDragArmRequest request));
        Assert.IsTrue(request.Gesture.StartedOnArtwork);
        Assert.IsTrue(request.Gesture.IsPrimary);
        Assert.IsTrue(request.Gesture.IsMouse);
        Assert.IsFalse(_parser.TryParseDirectDragArmRequest(
            stale,
            BridgeToken,
            out _));
        Assert.IsFalse(_parser.TryParseDirectDragArmRequest(
            extra,
            BridgeToken,
            out _));
        Assert.IsFalse(new PreviewNavigationPolicy()
            .IsTrustedWebMessageSource("https://example.test"));
    }

    [TestMethod]
    public void DirectDragSignal_RequiresExactStartOrCancelSchema()
    {
        const string start = """
            {"type":"directDrag",
             "token":"00112233445566778899AABBCCDDEEFF",
             "action":"start",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":260,"y":125,"viewportWidth":500,"viewportHeight":250}
            """;
        const string cancel = """
            {"type":"directDrag",
             "token":"00112233445566778899AABBCCDDEEFF",
             "action":"cancel",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":250,"y":125,"viewportWidth":500,"viewportHeight":250}
            """;
        const string outside = """
            {"type":"directDrag",
             "token":"00112233445566778899AABBCCDDEEFF",
             "action":"start",
             "gestureId":"FFEEDDCCBBAA99887766554433221100",
             "x":501,"y":125,"viewportWidth":500,"viewportHeight":250}
            """;

        Assert.IsTrue(_parser.TryParseDirectDragSignal(
            start,
            BridgeToken,
            out PreviewDirectDragSignal startSignal));
        Assert.AreEqual(
            PreviewDirectDragSignalAction.Start,
            startSignal.Action);
        Assert.IsTrue(_parser.TryParseDirectDragSignal(
            cancel,
            BridgeToken,
            out PreviewDirectDragSignal cancelSignal));
        Assert.AreEqual(
            PreviewDirectDragSignalAction.Cancel,
            cancelSignal.Action);
        Assert.IsFalse(_parser.TryParseDirectDragSignal(
            outside,
            BridgeToken,
            out _));
    }

    [TestMethod]
    public void AnchorScrollCalculation_KeepsPointerContentStableAndClamps()
    {
        PreviewZoomRequest centered = new(
            PreviewZoomDirection.In,
            ContentX: 0.5,
            ContentY: 0.5,
            AnchorX: 300,
            AnchorY: 200,
            ViewportWidth: 600,
            ViewportHeight: 400);

        PreviewScrollPosition position = _scrollCalculator.KeepAnchorStable(
            centered,
            contentWidth: 1200,
            contentHeight: 800);

        Assert.AreEqual(300, position.Left, 0.0001);
        Assert.AreEqual(200, position.Top, 0.0001);

        PreviewScrollPosition clamped = _scrollCalculator.KeepAnchorStable(
            centered with { ContentX = 1, ContentY = 1, AnchorX = 0, AnchorY = 0 },
            contentWidth: 1200,
            contentHeight: 800);
        Assert.AreEqual(600, clamped.Left, 0.0001);
        Assert.AreEqual(400, clamped.Top, 0.0001);
    }
}
