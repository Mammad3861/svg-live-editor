using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewVisualInteractionTests
{
    private const string Token =
        "00112233445566778899AABBCCDDEEFF";
    private const string Gesture =
        "FFEEDDCCBBAA99887766554433221100";
    private readonly PreviewVisualInteractionMessageParser _parser = new();

    [TestMethod]
    public void PointerMessageRequiresExactTokenRevisionAndSchema()
    {
        string valid = PointerJson();

        Assert.IsTrue(_parser.TryParsePointer(
            valid,
            Token,
            expectedSourceRevision: 7,
            out PreviewVisualPointerMessage message));
        Assert.AreEqual(PreviewVisualPointerPhase.Down, message.Phase);
        Assert.AreEqual(7, message.SourceRevision);

        Assert.IsFalse(_parser.TryParsePointer(
            valid,
            "11112233445566778899AABBCCDDEEFF",
            7,
            out _));
        Assert.IsFalse(_parser.TryParsePointer(
            valid,
            Token,
            8,
            out _));
        Assert.IsFalse(_parser.TryParsePointer(
            valid[..^1] + ",\"extra\":true}",
            Token,
            7,
            out _));
    }

    [TestMethod]
    public void PointerMessageRejectsSyntheticMismatchAndOutOfRangeCoordinates()
    {
        using JsonDocument document = JsonDocument.Parse(PointerJson());
        Dictionary<string, object?> values = document.RootElement
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ToValue(property.Value));

        values["pointerType"] = "touch";
        Assert.IsFalse(_parser.TryParsePointer(
            JsonSerializer.Serialize(values),
            Token,
            7,
            out _));

        values["pointerType"] = "mouse";
        values["x"] = 501;
        Assert.IsFalse(_parser.TryParsePointer(
            JsonSerializer.Serialize(values),
            Token,
            7,
            out _));
    }

    [TestMethod]
    public void ResizePointerRequiresExactTokenRevisionSelectionAndSchema()
    {
        string valid = ResizePointerJson();

        Assert.IsTrue(_parser.TryParseResizePointer(
            valid,
            Token,
            7,
            Gesture,
            out PreviewVisualResizePointerMessage message));
        Assert.AreEqual(SvgResizeHandle.BottomRight, message.Handle);
        Assert.AreEqual(Gesture, message.SelectionId);
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid,
            "11112233445566778899AABBCCDDEEFF",
            7,
            Gesture,
            out _));
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid,
            Token,
            8,
            Gesture,
            out _));
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid,
            Token,
            7,
            "11112233445566778899AABBCCDDEEFF",
            out _));
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid[..^1] + ",\"extra\":true}",
            Token,
            7,
            Gesture,
            out _));
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid.Replace(
                "\"handle\":\"bottom-right\"",
                "\"handle\":\"rotate\"",
                StringComparison.Ordinal),
            Token,
            7,
            Gesture,
            out _));
        Assert.IsFalse(_parser.TryParseResizePointer(
            valid.Replace(
                "\"isTrusted\":true",
                "\"isTrusted\":false",
                StringComparison.Ordinal),
            Token,
            7,
            Gesture,
            out _));
    }

    [TestMethod]
    public void NudgeAcceptsOnlyOneSupportedAxisAndCurrentRevision()
    {
        const string valid =
            """{"type":"visualNudge","token":"00112233445566778899AABBCCDDEEFF","sourceRevision":7,"deltaX":0,"deltaY":-10}""";

        Assert.IsTrue(_parser.TryParseNudge(
            valid,
            Token,
            7,
            out PreviewVisualNudgeRequest request));
        Assert.AreEqual(-10, request.DeltaY);
        Assert.IsFalse(_parser.TryParseNudge(
            valid.Replace("\"deltaX\":0", "\"deltaX\":1"),
            Token,
            7,
            out _));
        Assert.IsFalse(_parser.TryParseNudge(
            valid.Replace("\"deltaY\":-10", "\"deltaY\":-2"),
            Token,
            7,
            out _));
    }

    [TestMethod]
    public void VisualSelectionHostMessageHasExactBoundedSchema()
    {
        PreviewPageMessageBuilder builder = new();
        string json = builder.BuildVisualSelectionMessage(
            Token,
            7,
            new PreviewVisualSelection(
                SvgVisualElementKind.Circle,
                new SvgVisualShapeGeometry(
                    SvgVisualElementKind.Circle,
                    10,
                    20,
                    30,
                    40),
                2,
                -3,
                Gesture,
                [
                    new SvgResizeHandleDefinition(
                        SvgResizeHandle.Right,
                        new SvgVisualPoint(30, 30))
                ]));
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.AreEqual(
            13,
            document.RootElement.EnumerateObject().Count());
        Assert.AreEqual(
            "circle",
            document.RootElement.GetProperty("kind").GetString());
        Assert.AreEqual(
            7,
            document.RootElement.GetProperty("sourceRevision").GetInt64());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            builder.BuildVisualSelectionMessage(
                Token,
                7,
                new PreviewVisualSelection(
                    SvgVisualElementKind.Rect,
                    new SvgVisualShapeGeometry(
                        SvgVisualElementKind.Rect,
                        double.PositiveInfinity,
                        0,
                        1,
                        1),
                    0,
                    0,
                    Gesture,
                    [])));
    }

    [TestMethod]
    public void InspectOnlyUnsupportedBoundsUseTheFixedRectangleOverlay()
    {
        string json = new PreviewPageMessageBuilder()
            .BuildVisualSelectionMessage(
                Token,
                7,
                new PreviewVisualSelection(
                    SvgVisualElementKind.Unsupported,
                    new SvgVisualShapeGeometry(
                        SvgVisualElementKind.Unsupported,
                        10,
                        20,
                        30,
                        40),
                    0,
                    0,
                    Gesture,
                    []));
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.AreEqual(
            "rect",
            document.RootElement.GetProperty("kind").GetString());
    }

    [TestMethod]
    public void VisualSelectionRejectsMismatchedOrDuplicateResizeHandles()
    {
        PreviewPageMessageBuilder builder = new();
        SvgVisualShapeGeometry geometry = new(
            SvgVisualElementKind.Rect,
            10,
            20,
            30,
            40);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            builder.BuildVisualSelectionMessage(
                Token,
                7,
                new PreviewVisualSelection(
                    SvgVisualElementKind.Rect,
                    geometry,
                    0,
                    0,
                    Gesture,
                    [
                        new SvgResizeHandleDefinition(
                            SvgResizeHandle.Start,
                            new SvgVisualPoint(10, 20))
                    ])));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            builder.BuildVisualSelectionMessage(
                Token,
                7,
                new PreviewVisualSelection(
                    SvgVisualElementKind.Rect,
                    geometry,
                    0,
                    0,
                    Gesture,
                    [
                        new SvgResizeHandleDefinition(
                            SvgResizeHandle.Left,
                            new SvgVisualPoint(10, 30)),
                        new SvgResizeHandleDefinition(
                            SvgResizeHandle.Left,
                            new SvgVisualPoint(10, 30))
                    ])));
    }

    [TestMethod]
    public void TrustedPageCancelsResizeAndKeepsPngLimitedToTheImage()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>";
        string html = new PreviewHtmlBuilder().Build(
            source,
            300,
            150,
            Token);

        StringAssert.Contains(html, "stopResizeGesture(event)");
        StringAssert.Contains(html, "pointercancel");
        StringAssert.Contains(html, "lostpointercapture");
        StringAssert.Contains(html, "pointerleave");
        StringAssert.Contains(html, "window.addEventListener('blur'");
        StringAssert.Contains(html, "event.code === 'Escape'");
        StringAssert.Contains(html, "context.drawImage(image");
        Assert.IsFalse(html.Contains(
            "context.drawImage(resizeHandleLayer",
            StringComparison.Ordinal));

        int panPriority = html.IndexOf(
            "spaceHeld || event.ctrlKey || panModeEnabled",
            StringComparison.Ordinal);
        int resizeChoice = html.IndexOf(
            "return 'resize';",
            StringComparison.Ordinal);
        int altDrag = html.IndexOf(
            "return 'drag';",
            StringComparison.Ordinal);
        Assert.IsTrue(panPriority >= 0 && panPriority < resizeChoice);
        Assert.IsTrue(altDrag >= 0 && altDrag < resizeChoice);
    }

    [TestMethod]
    public void ReadinessRejectsPanInvalidPendingAndStaleRevisions()
    {
        VisualEditingReadinessPolicy policy = new();
        VisualEditingReadiness ready = new(
            IsPanModeEnabled: false,
            IsCurrentSourceValid: true,
            IsInspectorIndexCurrent: true,
            InspectorRevision: 5,
            CurrentSourceRevision: 5,
            LastValidVisualRevision: 5,
            VisiblePreviewRevision: 5,
            IsNavigationPending: false);

        Assert.IsTrue(policy.IsReady(ready));
        Assert.IsFalse(policy.IsReady(ready with
        {
            IsPanModeEnabled = true
        }));
        Assert.IsFalse(policy.IsReady(ready with
        {
            IsCurrentSourceValid = false
        }));
        Assert.IsFalse(policy.IsReady(ready with
        {
            IsNavigationPending = true
        }));
        Assert.IsFalse(policy.IsReady(ready with
        {
            VisiblePreviewRevision = 4
        }));
    }

    [TestMethod]
    public void NudgeRoutesOnlyFromActualPreviewKeyboardFocus()
    {
        Assert.IsTrue(PreviewVisualNudgeFocusPolicy.CanRoute(
            previewHasKeyboardFocus: true,
            sourceEditorHasKeyboardFocus: false,
            propertyFieldHasKeyboardFocus: false));
        Assert.IsFalse(PreviewVisualNudgeFocusPolicy.CanRoute(
            previewHasKeyboardFocus: false,
            sourceEditorHasKeyboardFocus: true,
            propertyFieldHasKeyboardFocus: false));
        Assert.IsFalse(PreviewVisualNudgeFocusPolicy.CanRoute(
            previewHasKeyboardFocus: false,
            sourceEditorHasKeyboardFocus: false,
            propertyFieldHasKeyboardFocus: true));
        Assert.IsFalse(PreviewVisualNudgeFocusPolicy.CanRoute(
            previewHasKeyboardFocus: false,
            sourceEditorHasKeyboardFocus: false,
            propertyFieldHasKeyboardFocus: false));
    }

    [TestMethod]
    public void PreviewNavigationOriginCanNavigateOnlyCurrentSourceSpan()
    {
        InspectorSelectionCoordinator coordinator = new();

        Assert.IsTrue(coordinator.TryGetNavigationSpan(
            InspectorSelectionOrigin.PreviewNavigation,
            new SourceSpan(10, 5),
            isIndexCurrent: true,
            indexRevision: 4,
            sourceRevision: 4,
            isEditorTextCompositionActive: false,
            documentLength: 100,
            out SourceSpan current));
        Assert.AreEqual(new SourceSpan(10, 5), current);
        Assert.IsFalse(coordinator.TryGetNavigationSpan(
            InspectorSelectionOrigin.PreviewNavigation,
            new SourceSpan(10, 5),
            isIndexCurrent: true,
            indexRevision: 3,
            sourceRevision: 4,
            isEditorTextCompositionActive: false,
            documentLength: 100,
            out _));
    }

    [TestMethod]
    public void PreviewShellKeepsBase64IsolationAndExactHashCsp()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"1\" y=\"2\" width=\"3\" height=\"4\"/></svg>";
        SvgDocumentIndex index =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            index,
            new SvgCanvasSizeReader().Read(source));

        string html = new PreviewHtmlBuilder().Build(
            source,
            300,
            150,
            Token,
            PreviewViewportPosition.Center,
            sourceRevision: 9,
            visual.Viewport);

        StringAssert.Contains(
            html,
            "img-src data:");
        StringAssert.Contains(
            html,
            "script-src 'sha256-");
        StringAssert.Contains(
            html,
            "data:image/svg+xml;base64,");
        StringAssert.Contains(
            html,
            "class=\"selection-overlay\"");
        StringAssert.Contains(
            html,
            "Object.keys(message).length === 13");
        StringAssert.Contains(html, "visualResizePointer");
        StringAssert.Contains(html, "selectionId");
        StringAssert.Contains(html, "resize-handle-layer");
        StringAssert.Contains(
            html,
            "message.sourceRevision === sourceRevision");
        Assert.IsFalse(
            html.Contains("<rect x=\"1\"", StringComparison.Ordinal));
        Assert.IsFalse(
            html.Contains("unsafe-eval", StringComparison.Ordinal));
        Assert.IsFalse(
            html.Contains(
                "unsafe-inline'; script-src",
                StringComparison.Ordinal));
    }

    private static string PointerJson() =>
        $$"""
        {"type":"visualPointer","token":"{{Token}}","sourceRevision":7,
         "phase":"down","gestureId":"{{Gesture}}",
         "x":25,"y":30,"viewportWidth":500,"viewportHeight":300,
         "imageLeft":10,"imageTop":15,"imageWidth":400,"imageHeight":200,
         "button":0,"buttons":1,"ctrlKey":false,"shiftKey":false,
         "altKey":false,"metaKey":false,"spaceHeld":false,
         "pointerType":"mouse","isPrimary":true}
        """;

    private static string ResizePointerJson() =>
        $$"""
        {"type":"visualResizePointer","token":"{{Token}}","sourceRevision":7,
         "selectionId":"{{Gesture}}","phase":"down",
         "gestureId":"0011AABBCCDDEEFF9988776655443322",
         "handle":"bottom-right","x":25,"y":30,
         "viewportWidth":500,"viewportHeight":300,
         "imageLeft":10,"imageTop":15,"imageWidth":400,"imageHeight":200,
         "button":0,"buttons":1,"ctrlKey":false,"shiftKey":false,
         "altKey":false,"metaKey":false,"spaceHeld":false,
         "pointerType":"mouse","isTrusted":true,"isPrimary":true}
        """;

    private static object? ToValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out long integer) =>
                integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
