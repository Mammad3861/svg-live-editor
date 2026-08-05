using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PreviewBridgeIntegrationTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private sealed record WheelResult(
        bool HorizontalPositiveScrolled,
        bool HorizontalNegativeScrolled,
        bool HorizontalPreservedVertical,
        bool FractionalHorizontalPreserved,
        bool ClampedAtBothEnds,
        bool NoOverflowDidNotMove,
        bool VerticalScrolled,
        bool VerticalPreservedHorizontal,
        bool DiagonalScrolledBothAxes,
        bool ShiftFallbackScrolledHorizontally,
        bool ShiftNativeHorizontalWasNotDoubled,
        bool LineModeNormalized,
        bool PageModeNormalized,
        bool MalformedDeltaIgnored,
        bool NativeHostMessageScrolledOnce,
        bool NativeHostMessagePreservedVertical,
        bool StaleAndExtraNativeMessagesIgnored,
        bool CtrlWheelCanceled,
        bool CtrlWheelDidNotScroll,
        bool OrdinaryWheelDidNotZoom);

    private sealed record BridgeResult(
        string Direction,
        string DisplayText,
        double ImageWidth,
        double ImageHeight,
        double WebViewZoomFactor,
        string BackgroundSize,
        bool InitialImageLoadMessageValidated,
        int ZoomMessageCount,
        WheelResult Wheel,
        bool SourceRefreshPreservedViewport,
        int PngWidth,
        int PngHeight,
        bool PngTopLeftIsTransparent,
        int ZoomNavigationCount,
        bool ContextMenuCanceled,
        bool ContextMenuParsed,
        bool PlainCopyCanceled,
        int CopyCommandCount,
        bool VisualSelectionStayedTokenAndRevisionBound,
        bool EnglishTextMeasured,
        bool PersianTextMeasured,
        bool TextOverlayAligned,
        bool TextBoundsMatchBrowserSvg,
        bool PersianLtrHitMatchesVisibleText,
        bool OldMirroredPersianLocationDoesNotHit,
        bool TextMeasurementDidNotModifySource,
        bool MeasurementSurfaceWasRemoved,
        bool InvalidTextMeasurementRequestsWereIgnored,
        bool FontChangeMeasured,
        bool ResizeMessagesPassedStrictParser,
        bool ResizeTemporaryOutlineChanged,
        bool ResizeSourceStayedUnchangedBeforeRelease,
        bool ResizeCommittedExactlyOnce,
        bool ResizeRenderedArtworkUpdated,
        bool ResizeHandleStayedFixedSize,
        bool ResizeModifiersPreservedArbitration,
        bool ResizeDidNotNavigateDuringPointerMove,
        bool ResizeKeptWebViewZoomAtOne,
        bool ArrangeChangedTopmostHitAndUndoRestoredIt,
        bool OpacityCommittedAndUndoRestoredSource,
        bool StageTwoHostEditsDidNotNavigate,
        bool StageTwoPreviewStayedReadyAtDocumentZoomOne);

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task TrustedPage_DomWheelAndNativeHostScroll_StaySingleAndIsolated()
    {
        TaskCompletionSource<BridgeResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = RunHarnessAsync(completion);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        BridgeResult result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual("in", result.Direction);
        Assert.AreEqual("125%", result.DisplayText);
        Assert.AreEqual(375, result.ImageWidth, 0.5);
        Assert.AreEqual(187.5, result.ImageHeight, 0.5);
        Assert.AreEqual(1.0, result.WebViewZoomFactor, 0.0001);
        Assert.AreEqual(
            "24px 24px, 24px 24px, 24px 24px, 24px 24px",
            result.BackgroundSize);
        Assert.IsTrue(result.InitialImageLoadMessageValidated);
        Assert.AreEqual(1, result.ZoomMessageCount);
        Assert.IsTrue(result.Wheel.HorizontalPositiveScrolled);
        Assert.IsTrue(result.Wheel.HorizontalNegativeScrolled);
        Assert.IsTrue(result.Wheel.HorizontalPreservedVertical);
        Assert.IsTrue(result.Wheel.FractionalHorizontalPreserved);
        Assert.IsTrue(result.Wheel.ClampedAtBothEnds);
        Assert.IsTrue(result.Wheel.NoOverflowDidNotMove);
        Assert.IsTrue(result.Wheel.VerticalScrolled);
        Assert.IsTrue(result.Wheel.VerticalPreservedHorizontal);
        Assert.IsTrue(result.Wheel.DiagonalScrolledBothAxes);
        Assert.IsTrue(result.Wheel.ShiftFallbackScrolledHorizontally);
        Assert.IsTrue(result.Wheel.ShiftNativeHorizontalWasNotDoubled);
        Assert.IsTrue(result.Wheel.LineModeNormalized);
        Assert.IsTrue(result.Wheel.PageModeNormalized);
        Assert.IsTrue(result.Wheel.MalformedDeltaIgnored);
        Assert.IsTrue(result.Wheel.NativeHostMessageScrolledOnce);
        Assert.IsTrue(result.Wheel.NativeHostMessagePreservedVertical);
        Assert.IsTrue(result.Wheel.StaleAndExtraNativeMessagesIgnored);
        Assert.IsTrue(result.Wheel.CtrlWheelCanceled);
        Assert.IsTrue(result.Wheel.CtrlWheelDidNotScroll);
        Assert.IsTrue(result.Wheel.OrdinaryWheelDidNotZoom);
        Assert.IsTrue(result.SourceRefreshPreservedViewport);
        Assert.AreEqual(300, result.PngWidth);
        Assert.AreEqual(150, result.PngHeight);
        Assert.IsTrue(result.PngTopLeftIsTransparent);
        Assert.AreEqual(0, result.ZoomNavigationCount);
        Assert.IsTrue(result.ContextMenuCanceled);
        Assert.IsTrue(result.ContextMenuParsed);
        Assert.IsTrue(result.PlainCopyCanceled);
        Assert.AreEqual(1, result.CopyCommandCount);
        Assert.IsTrue(
            result.VisualSelectionStayedTokenAndRevisionBound);
        Assert.IsTrue(result.EnglishTextMeasured);
        Assert.IsTrue(result.PersianTextMeasured);
        Assert.IsTrue(result.TextOverlayAligned);
        Assert.IsTrue(result.TextBoundsMatchBrowserSvg);
        Assert.IsTrue(result.PersianLtrHitMatchesVisibleText);
        Assert.IsTrue(result.OldMirroredPersianLocationDoesNotHit);
        Assert.IsTrue(result.TextMeasurementDidNotModifySource);
        Assert.IsTrue(result.MeasurementSurfaceWasRemoved);
        Assert.IsTrue(result.InvalidTextMeasurementRequestsWereIgnored);
        Assert.IsTrue(result.FontChangeMeasured);
        Assert.IsTrue(result.ResizeMessagesPassedStrictParser);
        Assert.IsTrue(result.ResizeTemporaryOutlineChanged);
        Assert.IsTrue(result.ResizeSourceStayedUnchangedBeforeRelease);
        Assert.IsTrue(result.ResizeCommittedExactlyOnce);
        Assert.IsTrue(result.ResizeRenderedArtworkUpdated);
        Assert.IsTrue(result.ResizeHandleStayedFixedSize);
        Assert.IsTrue(result.ResizeModifiersPreservedArbitration);
        Assert.IsTrue(result.ResizeDidNotNavigateDuringPointerMove);
        Assert.IsTrue(result.ResizeKeptWebViewZoomAtOne);
        Assert.IsTrue(result.ArrangeChangedTopmostHitAndUndoRestoredIt);
        Assert.IsTrue(result.OpacityCommittedAndUndoRestoredSource);
        Assert.IsTrue(result.StageTwoHostEditsDidNotNavigate);
        Assert.IsTrue(result.StageTwoPreviewStayedReadyAtDocumentZoomOne);
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<BridgeResult> completion)
    {
        string userDataFolder = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.Tests",
            Guid.NewGuid().ToString("N"));
        Window? window = null;
        WebView2? webView = null;

        try
        {
            window = new Window
            {
                Title = "SvgLiveEditor bridge integration",
                Width = 640,
                Height = 480,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow
            };
            webView = new WebView2();
            window.Content = webView;
            window.Show();

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);
            CoreWebView2 core = webView.CoreWebView2
                ?? throw new InvalidOperationException("WebView2 did not initialize.");
            core.Settings.IsScriptEnabled = true;
            core.Settings.IsWebMessageEnabled = true;
            // This must be true or WebView2 suppresses physical Ctrl+Wheel before
            // the trusted page can capture and replace native document zoom.
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.IsPinchZoomEnabled = false;
            webView.ZoomFactor = 1.0;

            TaskCompletionSource<(string Source, string Json)>
                initialImageStateReceived = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            core.WebMessageReceived += (_, args) =>
            {
                try
                {
                    using JsonDocument message =
                        JsonDocument.Parse(args.WebMessageAsJson);
                    if (message.RootElement.TryGetProperty(
                            "type",
                            out JsonElement type)
                        && type.GetString() == "imageState")
                    {
                        initialImageStateReceived.TrySetResult(
                            (args.Source, args.WebMessageAsJson));
                    }
                }
                catch (JsonException)
                {
                    // The production parser remains the authority for acceptance.
                }
            };

            PreviewHtmlBuilder htmlBuilder = new();
            SvgVisualViewport visualViewport = new(
                0,
                0,
                300,
                150,
                SvgPreserveAspectRatio.Default);
            const string svg =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><rect width=\"300\" height=\"150\" fill=\"white\" /></svg>";
            PreviewViewportPosition preservedViewport = new(0.75, 0.75);
            await NavigateAsync(
                core,
                htmlBuilder.Build(
                    svg,
                    1200,
                    600,
                    BridgeToken,
                    preservedViewport,
                    sourceRevision: 7,
                    visualViewport: visualViewport));
            (string imageStateSource, string imageStateJson) =
                await initialImageStateReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            bool initialImageLoadMessageValidated =
                new PreviewNavigationPolicy().IsTrustedWebMessageSource(
                    imageStateSource)
                && new PreviewInteractionMessageParser()
                    .TryParseImageLoadState(
                        imageStateJson,
                        BridgeToken,
                        expectedSourceRevision: 7,
                        out PreviewImageLoadMessage initialImageState)
                && initialImageState.State == PreviewImageLoadState.Loaded
                && initialImageState.NaturalWidth == 300
                && initialImageState.NaturalHeight == 150
                && NearlyEqual(webView.ZoomFactor, 1.0);
            string hostScriptReady = await core.ExecuteScriptAsync(
                "document.body.dataset.hostScriptReady || 'false'");
            if (!hostScriptReady.Equals("\"true\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The CSP-authorized host script did not run: {hostScriptReady}");
            }

            await Task.Delay(100);
            const string refreshedSvg =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><circle cx=\"150\" cy=\"75\" r=\"70\" fill=\"blue\" /></svg>";
            await NavigateAsync(
                core,
                htmlBuilder.Build(
                    refreshedSvg,
                    1800,
                    900,
                    BridgeToken,
                    preservedViewport,
                    sourceRevision: 7,
                    visualViewport: visualViewport));
            await Task.Delay(100);
            string restoredJson = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    """
                    JSON.stringify((() => {
                      const viewport = document.querySelector('.preview-viewport');
                      return {
                        left: viewport.scrollLeft,
                        top: viewport.scrollTop,
                        centerX: (viewport.scrollLeft + viewport.clientWidth / 2) / viewport.scrollWidth,
                        centerY: (viewport.scrollTop + viewport.clientHeight / 2) / viewport.scrollHeight
                      };
                    })())
                    """))
                ?? throw new InvalidOperationException("WebView2 returned no viewport metrics.");
            using JsonDocument restoredMetrics = JsonDocument.Parse(restoredJson);
            JsonElement restored = restoredMetrics.RootElement;
            bool sourceRefreshPreservedViewport =
                restored.GetProperty("left").GetDouble() > 0
                && restored.GetProperty("top").GetDouble() > 0
                && Math.Abs(restored.GetProperty("centerX").GetDouble() - 0.75) < 0.02
                && Math.Abs(restored.GetProperty("centerY").GetDouble() - 0.75) < 0.02;

            PreviewPageMessageBuilder visualMessageBuilder = new();
            core.PostWebMessageAsJson(
                visualMessageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    sourceRevision: 7,
                    new PreviewVisualSelection(
                        SvgVisualElementKind.Rect,
                        new SvgVisualShapeGeometry(
                            SvgVisualElementKind.Rect,
                            10,
                            20,
                            110,
                            70),
                        5,
                        -5,
                        "0123456789ABCDEFFEDCBA9876543210",
                        [
                            new SvgResizeHandleDefinition(
                                SvgResizeHandle.BottomRight,
                                new SvgVisualPoint(110, 70))
                        ])));
            await Task.Delay(50);
            string acceptedVisualSelection =
                await ReadVisualSelectionAsync(core);
            core.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "visualSelection",
                token = BridgeToken,
                sourceRevision = 6,
                visible = false,
                kind = "none",
                x1 = 0,
                y1 = 0,
                x2 = 0,
                y2 = 0,
                deltaX = 0,
                deltaY = 0
            }));
            core.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "visualSelection",
                token = BridgeToken,
                sourceRevision = 7,
                visible = false,
                kind = "none",
                x1 = 0,
                y1 = 0,
                x2 = 0,
                y2 = 0,
                deltaX = 0,
                deltaY = 0,
                extra = true
            }));
            await Task.Delay(50);
            string rejectedVisualSelection =
                await ReadVisualSelectionAsync(core);
            bool visualSelectionStayedTokenAndRevisionBound =
                acceptedVisualSelection.Equals(
                    "rect|15|15|100|50",
                    StringComparison.Ordinal)
                && rejectedVisualSelection.Equals(
                    acceptedVisualSelection,
                    StringComparison.Ordinal);

            TaskCompletionSource<(string Source, string Json)> messageReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(string Source, string Json)> pngMessageReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(string Source, string Json)> contextMenuReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(string Source, string Json)> copyCommandReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> firstTextMeasurementReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> secondTextMeasurementReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> resizeDownReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> resizeMoveReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> resizeUpReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int zoomMessageCount = 0;
            int copyCommandCount = 0;
            int textMeasurementMessageCount = 0;
            int zoomNavigationCount = 0;
            core.NavigationStarting += (_, _) => zoomNavigationCount++;
            core.WebMessageReceived += (_, args) =>
            {
                using JsonDocument message = JsonDocument.Parse(args.WebMessageAsJson);
                if (message.RootElement.TryGetProperty("type", out JsonElement type)
                    && type.GetString() == "zoom")
                {
                    zoomMessageCount++;
                    messageReceived.TrySetResult((args.Source, args.WebMessageAsJson));
                }
                else if (message.RootElement.TryGetProperty(
                         "type",
                         out type)
                    && type.GetString() == "png")
                {
                    pngMessageReceived.TrySetResult(
                        (args.Source, args.WebMessageAsJson));
                }
                else if (message.RootElement.TryGetProperty(
                         "type",
                         out type)
                    && type.GetString() == "contextMenu")
                {
                    contextMenuReceived.TrySetResult(
                        (args.Source, args.WebMessageAsJson));
                }
                else if (message.RootElement.TryGetProperty(
                         "type",
                         out type)
                    && type.GetString() == "copyCommand")
                {
                    copyCommandCount++;
                    copyCommandReceived.TrySetResult(
                        (args.Source, args.WebMessageAsJson));
                }
                else if (message.RootElement.TryGetProperty(
                         "type",
                         out type)
                    && type.GetString() == "textMeasurements")
                {
                    textMeasurementMessageCount++;
                    (textMeasurementMessageCount == 1
                        ? firstTextMeasurementReceived
                        : secondTextMeasurementReceived)
                        .TrySetResult(args.WebMessageAsJson);
                }
                else if (message.RootElement.TryGetProperty(
                         "type",
                         out type)
                    && type.GetString() == "visualResizePointer"
                    && message.RootElement.TryGetProperty(
                        "phase",
                        out JsonElement phase))
                {
                    string json = args.WebMessageAsJson;
                    if (phase.GetString() == "down")
                    {
                        resizeDownReceived.TrySetResult(json);
                    }
                    else if (phase.GetString() == "move")
                    {
                        resizeMoveReceived.TrySetResult(json);
                    }
                    else if (phase.GetString() == "up")
                    {
                        resizeUpReceived.TrySetResult(json);
                    }
                }
            };

            PreviewPageMessageBuilder textMessageBuilder = new();
            const string textRequestId =
                "11223344556677889900AABBCCDDEEFF";
            SvgVisualTextMeasurementSpec[] textItems =
            [
                new(
                    0,
                    "English text",
                    30,
                    60,
                    24,
                    "Segoe UI, sans-serif",
                    "400",
                    "normal",
                    "start",
                    "ltr",
                    "normal"),
                new(
                    1,
                    "سلام SVG، نسخه ۶.",
                    280,
                    110,
                    26,
                    "Tahoma, sans-serif",
                    "700",
                    "normal",
                    "end",
                    "rtl",
                    "plaintext"),
                new(
                    2,
                    "بهروز",
                    128,
                    244,
                    72,
                    "\"Segoe UI\", sans-serif",
                    "700",
                    "normal",
                    "start",
                    "ltr",
                    "plaintext"),
                new(
                    3,
                    "بهروز",
                    128,
                    244,
                    72,
                    "\"Segoe UI\", sans-serif",
                    "700",
                    "normal",
                    "start",
                    "rtl",
                    "plaintext"),
                new(
                    4,
                    "English text",
                    220,
                    80,
                    28,
                    "Segoe UI, sans-serif",
                    "400",
                    "normal",
                    "start",
                    "rtl",
                    "plaintext"),
                new(
                    5,
                    "بهروز English",
                    180,
                    120,
                    30,
                    "Tahoma, sans-serif",
                    "400",
                    "normal",
                    "start",
                    "ltr",
                    "plaintext"),
                new(
                    6,
                    "English بهروز",
                    180,
                    160,
                    30,
                    "Tahoma, sans-serif",
                    "400",
                    "italic",
                    "start",
                    "rtl",
                    "plaintext"),
                new(
                    7,
                    "بهروز 123",
                    160,
                    200,
                    32,
                    "Tahoma, sans-serif",
                    "700",
                    "normal",
                    "start",
                    "ltr",
                    "plaintext"),
                new(
                    8,
                    "بهروز ۱۲۳: (آزمون).؟!",
                    260,
                    240,
                    32,
                    "Tahoma, sans-serif",
                    "700",
                    "normal",
                    "start",
                    "rtl",
                    "plaintext"),
                new(
                    9,
                    "بهروز (۱۲۳): تست.?!",
                    200,
                    280,
                    30,
                    "Tahoma, sans-serif",
                    "400",
                    "normal",
                    "middle",
                    "rtl",
                    "plaintext"),
                new(
                    10,
                    "بهروز",
                    180,
                    310,
                    30,
                    "Tahoma, sans-serif",
                    "400",
                    "normal",
                    "middle",
                    "ltr",
                    "plaintext"),
                new(
                    11,
                    "بهروز",
                    220,
                    340,
                    30,
                    "Tahoma, sans-serif",
                    "400",
                    "normal",
                    "end",
                    "ltr",
                    "plaintext"),
                new(
                    12,
                    "بهروز fallback",
                    140,
                    370,
                    30,
                    "\"SvgLiveEditor Missing Font\", \"Segoe UI\", sans-serif",
                    "400",
                    "normal",
                    "start",
                    "ltr",
                    "plaintext")
            ];
            core.PostWebMessageAsJson(
                textMessageBuilder.BuildTextMeasurementMessage(
                    BridgeToken,
                    7,
                    textRequestId,
                    textItems));
            string textMeasurementJson =
                await firstTextMeasurementReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
            PendingPreviewTextMeasurement textPending = new(
                BridgeToken,
                7,
                textRequestId,
                textItems.Select(item => item.Index).ToArray());
            bool textParsed =
                new PreviewTextMeasurementMessageParser().TryParse(
                    textMeasurementJson,
                    textPending,
                    out IReadOnlyList<SvgVisualTextMeasurementResult>
                        textResults);
            SvgVisualTextMeasurementResult? english =
                textResults.FirstOrDefault(result => result.Index == 0);
            SvgVisualTextMeasurementResult? persian =
                textResults.FirstOrDefault(result => result.Index == 1);
            SvgVisualTextMeasurementResult? exactPersianLtr =
                textResults.FirstOrDefault(result => result.Index == 2);
            bool englishTextMeasured =
                textParsed && english is { IsSuccess: true, Bounds: not null }
                && english.Bounds.Value.Left >= 25
                && english.Bounds.Value.Right > english.Bounds.Value.Left;
            bool persianTextMeasured =
                textParsed && persian is { IsSuccess: true, Bounds: not null }
                && double.IsFinite(persian.Bounds.Value.Left)
                && double.IsFinite(persian.Bounds.Value.Right)
                && persian.Bounds.Value.Right > persian.Bounds.Value.Left;

            IReadOnlyDictionary<int, SvgVisualBounds> browserBounds =
                await MeasureReferenceTextBoundsAsync(core, textItems);
            bool textBoundsMatchBrowserSvg = textParsed
                && textResults.All(result =>
                    result is { IsSuccess: true, Bounds: not null }
                    && browserBounds.TryGetValue(
                        result.Index,
                        out SvgVisualBounds expected)
                    && BoundsMatch(
                        result.Bounds.Value,
                        expected,
                        tolerance: 0.25));
            int bodyMeasurementSurfaces = (int)ParseScriptNumber(
                await core.ExecuteScriptAsync(
                    "document.querySelectorAll('body > svg').length"));
            bool measurementSurfaceWasRemoved =
                bodyMeasurementSurfaces == 0;

            const string exactPersianSource =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 500 400\"><text id=\"name\" x=\"128\" y=\"244\" direction=\"ltr\" unicode-bidi=\"plaintext\" text-anchor=\"start\" font-family=\"&quot;Segoe UI&quot;, sans-serif\" font-size=\"72\" font-weight=\"700\">بهروز</text></svg>";
            string exactPersianSnapshot = exactPersianSource;
            SvgDocumentIndexResult exactIndex =
                new SvgDocumentIndexService().Build(exactPersianSource);
            SvgVisualDocument exactPending =
                new SvgVisualGeometryIndexService().Build(
                    exactIndex.Document
                        ?? throw new InvalidOperationException(
                            exactIndex.IndexError),
                    new SvgCanvasSizeReader().Read(exactPersianSource),
                    exactPersianSource);
            SvgVisualBounds exactBounds = exactPersianLtr?.Bounds
                ?? throw new InvalidOperationException(
                    "The exact Persian LTR bounds were unavailable.");
            SvgVisualDocument exactMeasured =
                new SvgVisualTextMeasurementService().Apply(
                    exactPending,
                    [new SvgVisualTextMeasurementResult(
                        0,
                        true,
                        exactBounds)]);
            SvgVisualHitTestService hitTest = new();
            double exactCenterX =
                (exactBounds.Left + exactBounds.Right) / 2;
            double exactCenterY =
                (exactBounds.Top + exactBounds.Bottom) / 2;
            bool persianLtrHitMatchesVisibleText =
                hitTest.HitTest(
                    exactMeasured,
                    new SvgMappedPreviewPoint(
                        new SvgVisualPoint(exactCenterX, exactCenterY),
                        0))?.SourceElement.Id == "name";
            double oldMirroredCenterX = (2 * 128) - exactCenterX;
            bool oldMirroredPersianLocationDoesNotHit =
                hitTest.HitTest(
                    exactMeasured,
                    new SvgMappedPreviewPoint(
                        new SvgVisualPoint(
                            oldMirroredCenterX,
                            exactCenterY),
                        0)) is null;
            bool textMeasurementDidNotModifySource =
                exactPersianSource.Equals(
                    exactPersianSnapshot,
                    StringComparison.Ordinal);

            core.PostWebMessageAsJson(
                visualMessageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    7,
                    new PreviewVisualSelection(
                        SvgVisualElementKind.Text,
                        new SvgVisualShapeGeometry(
                            SvgVisualElementKind.Text,
                            exactBounds.Left,
                            exactBounds.Top,
                            exactBounds.Right,
                            exactBounds.Bottom),
                        0,
                        0,
                        "0123456789ABCDEFFEDCBA9876543210",
                        [])));
            await Task.Delay(50);
            string textOverlay = await ReadVisualSelectionAsync(core);
            bool textOverlayAligned = TryReadOverlayBounds(
                    textOverlay,
                    out SvgVisualBounds overlayBounds)
                && BoundsMatch(
                    overlayBounds,
                    browserBounds[2],
                    tolerance: 0.25);

            const string invalidTextRequestId =
                "33445566778899001122AABBCCDDEEFF";
            string invalidTextRequest =
                textMessageBuilder.BuildTextMeasurementMessage(
                    BridgeToken,
                    7,
                    invalidTextRequestId,
                    [textItems[0]]);
            core.PostWebMessageAsJson(invalidTextRequest.Replace(
                "\"sourceRevision\":7",
                "\"sourceRevision\":6",
                StringComparison.Ordinal));
            core.PostWebMessageAsJson(invalidTextRequest.Replace(
                BridgeToken,
                "10112233445566778899AABBCCDDEEFF",
                StringComparison.Ordinal));
            core.PostWebMessageAsJson(
                invalidTextRequest[..^1] + ",\"extra\":true}");
            core.PostWebMessageAsJson(invalidTextRequest.Replace(
                "\"unicodeBidi\":\"normal\"",
                "\"unicodeBidi\":\"normal\",\"extra\":true",
                StringComparison.Ordinal));
            await Task.Delay(75);
            bool invalidTextMeasurementRequestsWereIgnored =
                textMeasurementMessageCount == 1;

            const string fontChangeRequestId =
                "22334455667788990011AABBCCDDEEFF";
            core.PostWebMessageAsJson(
                textMessageBuilder.BuildTextMeasurementMessage(
                    BridgeToken,
                    7,
                    fontChangeRequestId,
                    [textItems[0] with
                    {
                        Index = 2,
                        FontFamily = "Tahoma, sans-serif"
                    }]));
            string fontChangeJson =
                await secondTextMeasurementReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
            bool fontChangeMeasured =
                new PreviewTextMeasurementMessageParser().TryParse(
                    fontChangeJson,
                    new PendingPreviewTextMeasurement(
                        BridgeToken,
                        7,
                        fontChangeRequestId,
                        [2]),
                    out IReadOnlyList<SvgVisualTextMeasurementResult>
                        fontResults)
                && fontResults.Single().IsSuccess;

            WheelResult wheel = await RunWheelInputChecksAsync(
                core,
                () => zoomMessageCount);

            const string pngRequestId =
                "FFEEDDCCBBAA99887766554433221100";
            PreviewPngSize requestedPngSize = new(300, 150);
            core.PostWebMessageAsJson(
                new PreviewPageMessageBuilder().BuildPngRequestMessage(
                    BridgeToken,
                    pngRequestId,
                    requestedPngSize));
            (string pngSource, string pngJson) =
                await pngMessageReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            if (!new PreviewNavigationPolicy()
                    .IsTrustedWebMessageSource(pngSource))
            {
                throw new InvalidOperationException(
                    $"Unexpected PNG WebMessage source: {pngSource}");
            }

            PendingPreviewPngRequest expectedPng = new(
                BridgeToken,
                pngRequestId,
                new PreviewPngCopyPlan(
                    requestedPngSize,
                    PreviewPngSourceState.CurrentValid),
                PreviewPngRequestPurpose.ClipboardCopy);
            if (!new PreviewPngMessageParser().TryParse(
                    pngJson,
                    expectedPng,
                    out PreviewPngPayload? pngPayload)
                || pngPayload is null)
            {
                throw new InvalidOperationException(
                    "Trusted page returned an invalid PNG response.");
            }
            bool pngTopLeftIsTransparent =
                IsTopLeftTransparent(pngPayload.Bytes);

            string contextMenuDispatch = await core.ExecuteScriptAsync(
                """
                document.querySelector('.preview-viewport').dispatchEvent(
                  new MouseEvent('contextmenu', {
                    clientX: 200,
                    clientY: 120,
                    bubbles: true,
                    cancelable: true
                  }))
                """);
            (string contextSource, string contextJson) =
                await contextMenuReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            PreviewNavigationPolicy navigationPolicy = new();
            PreviewInteractionMessageParser parser = new();
            bool contextMenuParsed =
                navigationPolicy.IsTrustedWebMessageSource(contextSource)
                && parser.TryParseContextMenuRequest(
                    contextJson,
                    BridgeToken,
                    out PreviewContextMenuRequest contextRequest)
                && contextRequest.X > 0
                && contextRequest.Y > 0
                && contextRequest.SourceRevision == 7
                && contextRequest.SelectionId.Equals(
                    "0123456789ABCDEFFEDCBA9876543210",
                    StringComparison.Ordinal);

            string copyDispatch = await core.ExecuteScriptAsync(
                """
                (() => {
                  const viewport = document.querySelector('.preview-viewport');
                  viewport.focus();
                  return viewport.dispatchEvent(new KeyboardEvent('keydown', {
                    code: 'KeyC',
                    ctrlKey: true,
                    bubbles: true,
                    cancelable: true
                  }));
                })()
                """);
            (string copySource, string copyJson) =
                await copyCommandReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            if (!navigationPolicy.IsTrustedWebMessageSource(copySource)
                || !parser.IsCopyCommand(copyJson, BridgeToken))
            {
                throw new InvalidOperationException(
                    $"Trusted page sent an invalid copy command: {copyJson}");
            }

            await core.ExecuteScriptAsync(
                """
                (() => {
                  const viewport = document.querySelector('.preview-viewport');
                  viewport.dispatchEvent(new KeyboardEvent('keydown', {
                    code: 'KeyC',
                    ctrlKey: true,
                    shiftKey: true,
                    bubbles: true,
                    cancelable: true
                  }));
                  viewport.dispatchEvent(new KeyboardEvent('keydown', {
                    code: 'KeyC',
                    ctrlKey: true,
                    altKey: true,
                    bubbles: true,
                    cancelable: true
                  }));
                })()
                """);
            await Task.Delay(50);

            (string source, string json) =
                await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(150);
            if (!navigationPolicy.IsTrustedWebMessageSource(source))
            {
                throw new InvalidOperationException(
                    $"Unexpected WebMessage source: {source}; current document: {core.Source}");
            }

            if (!parser.TryParseZoomRequest(
                    json,
                    BridgeToken,
                    out PreviewZoomRequest request))
            {
                throw new InvalidOperationException(
                    $"Trusted page sent an invalid message: {json}");
            }

            PreviewZoomTransition transition = new PreviewZoomBridge().Apply(
                PreviewZoomState.At100Percent,
                new SvgCanvasSize(300, 150),
                fitScale: 1.0,
                request);
            double contentWidth = Math.Max(
                request.ViewportWidth,
                transition.RenderedWidth + (PreviewZoomCalculator.CanvasPadding * 2));
            double contentHeight = Math.Max(
                request.ViewportHeight,
                transition.RenderedHeight + (PreviewZoomCalculator.CanvasPadding * 2));
            PreviewViewportPosition viewport = new PreviewViewportCalculator().Capture(
                transition.Scroll,
                contentWidth,
                contentHeight,
                request.ViewportWidth,
                request.ViewportHeight);
            core.PostWebMessageAsJson(
                new PreviewPageMessageBuilder().BuildZoomStateMessage(
                    BridgeToken,
                    transition.RenderedWidth,
                    transition.RenderedHeight,
                    viewport));
            await Task.Delay(100);

            string scriptResult = await core.ExecuteScriptAsync(
                """
                JSON.stringify({
                  width: document.querySelector('img').getBoundingClientRect().width,
                  height: document.querySelector('img').getBoundingClientRect().height,
                  backgroundSize: getComputedStyle(document.body).backgroundSize
                })
                """);
            string metricsJson = JsonSerializer.Deserialize<string>(scriptResult)
                ?? throw new InvalidOperationException("WebView2 returned no metrics.");
            using JsonDocument metrics = JsonDocument.Parse(metricsJson);
            JsonElement root = metrics.RootElement;

            const string resizeSelectionId =
                "AABBCCDDEEFF00112233445566778899";
            string resizeSource =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><rect id=\"target\" x=\"80\" y=\"50\" width=\"60\" height=\"30\" fill=\"#dc2626\"/></svg>";
            string resizeSourceSnapshot = resizeSource;
            SvgDocumentIndexResult resizeIndex =
                new SvgDocumentIndexService().Build(resizeSource);
            SvgVisualDocument resizeVisual =
                new SvgVisualGeometryIndexService().Build(
                    resizeIndex.Document!,
                    new SvgCanvasSizeReader().Read(resizeSource));
            SvgVisualElement resizeElement = resizeVisual.Elements.Single();
            SvgVisualResizeHandleService resizeHandleService = new();
            SvgVisualResizeService resizeService = new();
            core.PostWebMessageAsJson(
                visualMessageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    7,
                    new PreviewVisualSelection(
                        resizeElement.Kind,
                        resizeElement.Geometry!,
                        0,
                        0,
                        resizeSelectionId,
                        resizeHandleService.Create(resizeElement))));
            await Task.Delay(75);

            ResizeHandleMetrics handleAtFirstScale =
                await GetResizeHandleMetricsAsync(
                    core,
                    "bottom-right");
            core.PostWebMessageAsJson(
                new PreviewPageMessageBuilder().BuildZoomStateMessage(
                    BridgeToken,
                    750,
                    375,
                    PreviewViewportPosition.Center));
            await Task.Delay(100);
            ResizeHandleMetrics handleAtSecondScale =
                await GetResizeHandleMetricsAsync(
                    core,
                    "bottom-right");
            bool resizeHandleStayedFixedSize =
                NearlyEqual(
                    handleAtFirstScale.Width,
                    PreviewHtmlBuilder.ResizeHandleSizeCssPixels)
                && NearlyEqual(
                    handleAtFirstScale.Height,
                    PreviewHtmlBuilder.ResizeHandleSizeCssPixels)
                && NearlyEqual(
                    handleAtSecondScale.Width,
                    PreviewHtmlBuilder.ResizeHandleSizeCssPixels)
                && NearlyEqual(
                    handleAtSecondScale.Height,
                    PreviewHtmlBuilder.ResizeHandleSizeCssPixels);

            core.PostWebMessageAsJson(
                new PreviewPageMessageBuilder().BuildPanStateMessage(
                    BridgeToken,
                    enabled: false,
                    minimumHorizontalDragDistance: 4,
                    minimumVerticalDragDistance: 4));
            await Task.Delay(50);
            await DispatchMouseAsync(
                core,
                "mousePressed",
                handleAtSecondScale.CenterX,
                handleAtSecondScale.CenterY,
                buttons: 1,
                button: "left",
                modifiers: 1);
            await DispatchMouseAsync(
                core,
                "mouseReleased",
                handleAtSecondScale.CenterX,
                handleAtSecondScale.CenterY,
                buttons: 0,
                button: "left",
                modifiers: 1);
            await DispatchMouseAsync(
                core,
                "mousePressed",
                handleAtSecondScale.CenterX,
                handleAtSecondScale.CenterY,
                buttons: 1,
                button: "left",
                modifiers: 2);
            await DispatchMouseAsync(
                core,
                "mouseReleased",
                handleAtSecondScale.CenterX,
                handleAtSecondScale.CenterY,
                buttons: 0,
                button: "left",
                modifiers: 2);
            bool resizeModifiersPreservedArbitration =
                !resizeDownReceived.Task.IsCompleted;

            int navigationCountBeforeResize = zoomNavigationCount;
            await DispatchMouseAsync(
                core,
                "mousePressed",
                handleAtSecondScale.CenterX,
                handleAtSecondScale.CenterY,
                buttons: 1,
                button: "left");
            string resizeDownJson = await resizeDownReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            await DispatchMouseAsync(
                core,
                "mouseMoved",
                handleAtSecondScale.CenterX + 50,
                handleAtSecondScale.CenterY + 25,
                buttons: 1,
                button: "left");
            string resizeMoveJson = await resizeMoveReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

            PreviewVisualInteractionMessageParser resizeParser = new();
            bool parsedDown = resizeParser.TryParseResizePointer(
                resizeDownJson,
                BridgeToken,
                7,
                resizeSelectionId,
                out PreviewVisualResizePointerMessage resizeDown);
            bool parsedMove = resizeParser.TryParseResizePointer(
                resizeMoveJson,
                BridgeToken,
                7,
                resizeSelectionId,
                out PreviewVisualResizePointerMessage resizeMove);
            SvgVisualShapeGeometry temporaryGeometry =
                resizeElement.Geometry!;
            bool calculatedTemporary = parsedMove
                && new PreviewSvgCoordinateMapper().TryMap(
                    resizeVisual.Viewport,
                    resizeMove.Image,
                    resizeMove.ViewportPoint,
                    out SvgMappedPreviewPoint mappedResize)
                && resizeService.TryCalculate(
                    resizeElement,
                    resizeMove.Handle,
                    mappedResize.Point,
                    resizeMove.ShiftHeld,
                    out temporaryGeometry,
                    out _);
            if (!calculatedTemporary)
            {
                throw new InvalidOperationException(
                    "The trusted resize move could not be mapped.");
            }
            core.PostWebMessageAsJson(
                visualMessageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    7,
                    new PreviewVisualSelection(
                        resizeElement.Kind,
                        temporaryGeometry,
                        0,
                        0,
                        resizeSelectionId,
                        resizeHandleService.Create(
                            resizeElement,
                            temporaryGeometry))));
            await Task.Delay(75);
            bool resizeTemporaryOutlineChanged =
                !string.Equals(
                    await ReadVisualSelectionAsync(core),
                    "rect|80|50|60|30",
                    StringComparison.Ordinal);
            bool resizeSourceStayedUnchangedBeforeRelease =
                resizeSource.Equals(
                    resizeSourceSnapshot,
                    StringComparison.Ordinal);
            bool resizeDidNotNavigateDuringPointerMove =
                zoomNavigationCount == navigationCountBeforeResize;

            await DispatchMouseAsync(
                core,
                "mouseReleased",
                handleAtSecondScale.CenterX + 50,
                handleAtSecondScale.CenterY + 25,
                buttons: 0,
                button: "left");
            string resizeUpJson = await resizeUpReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            bool parsedUp = resizeParser.TryParseResizePointer(
                resizeUpJson,
                BridgeToken,
                7,
                resizeSelectionId,
                out PreviewVisualResizePointerMessage resizeUp);
            int resizeEditCount = 0;
            SvgAttributeEditResult resizeEdit = resizeService.CreateEdit(
                resizeSource,
                resizeElement,
                temporaryGeometry);
            if (parsedUp && resizeEdit.IsSuccess && resizeEdit.Edit is not null)
            {
                resizeSource = resizeEdit.Edit.Apply(resizeSource);
                resizeEditCount++;
            }
            bool resizeCommittedExactlyOnce =
                resizeEditCount == 1
                && !resizeSource.Equals(
                    resizeSourceSnapshot,
                    StringComparison.Ordinal);

            string resizedImageSource =
                "data:image/svg+xml;base64,"
                + Convert.ToBase64String(Encoding.UTF8.GetBytes(resizeSource));
            string serializedImageSource =
                JsonSerializer.Serialize(resizedImageSource);
            await core.ExecuteScriptAsync(
                $$"""
                (() => new Promise(resolve => {
                  const image = document.querySelector('img');
                  image.addEventListener('load', () => resolve(true),
                    { once: true });
                  image.src = {{serializedImageSource}};
                }))()
                """);
            bool resizeRenderedArtworkUpdated =
                JsonSerializer.Deserialize<bool>(
                    await core.ExecuteScriptAsync(
                        $"document.querySelector('img').src === {serializedImageSource}"));
            bool resizeMessagesPassedStrictParser =
                parsedDown
                && parsedMove
                && parsedUp
                && resizeDown.Handle == SvgResizeHandle.BottomRight
                && resizeUp.Handle == SvgResizeHandle.BottomRight;
            bool resizeKeptWebViewZoomAtOne =
                NearlyEqual(webView.ZoomFactor, 1.0);

            const string layerSource = """
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                  <rect id="bottom" x="10" y="10" width="60" height="60" fill="red"/>
                  <rect id="top" x="10" y="10" width="60" height="60" fill="blue"/>
                </svg>
                """;
            int navigationCountBeforeStageTwo = zoomNavigationCount;
            SvgDocumentIndexService stageTwoIndexService = new();
            SvgDocumentIndex layerIndex =
                stageTwoIndexService.Build(layerSource).Document!;
            SvgVisualGeometryIndexService geometryIndex = new();
            SvgVisualHitTestService stageTwoHitTest = new();
            SvgMappedPreviewPoint overlapPoint = new(
                new SvgVisualPoint(30, 30),
                0);
            string? hitBeforeArrange = stageTwoHitTest.HitTest(
                geometryIndex.Build(
                    layerIndex,
                    new SvgCanvasSize(100, 100),
                    layerSource),
                overlapPoint)?.SourceElement.Id;
            SvgElementNode topElement = layerIndex.Elements.Single(element =>
                element.Id == "top");
            SvgLayerOrderEditResult layerEdit = new SvgLayerOrderService().CreateEdit(
                layerSource,
                layerIndex,
                topElement,
                SvgLayerOrderCommand.SendToBack);
            ICSharpCode.AvalonEdit.Document.TextDocument layerText =
                new(layerSource);
            new AvalonEditDocumentEditService().Apply(
                layerText,
                layerEdit.Edit!);
            SvgDocumentIndex reorderedIndex =
                stageTwoIndexService.Build(layerText.Text).Document!;
            string? hitAfterArrange = stageTwoHitTest.HitTest(
                geometryIndex.Build(
                    reorderedIndex,
                    new SvgCanvasSize(100, 100),
                    layerText.Text),
                overlapPoint)?.SourceElement.Id;
            layerText.UndoStack.Undo();
            SvgDocumentIndex undoIndex =
                stageTwoIndexService.Build(layerText.Text).Document!;
            string? hitAfterUndo = stageTwoHitTest.HitTest(
                geometryIndex.Build(
                    undoIndex,
                    new SvgCanvasSize(100, 100),
                    layerText.Text),
                overlapPoint)?.SourceElement.Id;
            bool arrangeChangedTopmostHitAndUndoRestoredIt =
                hitBeforeArrange == "top"
                && hitAfterArrange == "bottom"
                && hitAfterUndo == "top"
                && layerText.Text.Equals(layerSource, StringComparison.Ordinal);

            SvgElementNode opacityElement = layerIndex.Elements.Single(element =>
                element.Id == "bottom");
            SvgAttributeEditResult opacityEdit = new SvgOpacityService().CreateEdit(
                layerSource,
                layerIndex,
                opacityElement,
                25);
            ICSharpCode.AvalonEdit.Document.TextDocument opacityText =
                new(layerSource);
            new AvalonEditDocumentEditService().Apply(
                opacityText,
                opacityEdit.Edit!);
            bool opacityWasCommitted =
                opacityText.Text.Contains("opacity=\"0.25\"", StringComparison.Ordinal);
            opacityText.UndoStack.Undo();
            bool opacityCommittedAndUndoRestoredSource =
                opacityWasCommitted
                && opacityText.Text.Equals(layerSource, StringComparison.Ordinal);
            bool stageTwoHostEditsDidNotNavigate =
                zoomNavigationCount == navigationCountBeforeStageTwo;
            bool stageTwoPreviewStayedReadyAtDocumentZoomOne =
                JsonSerializer.Deserialize<bool>(
                    await core.ExecuteScriptAsync(
                        "document.querySelector('img').complete && document.querySelector('img').naturalWidth > 0"))
                && NearlyEqual(webView.ZoomFactor, 1.0);

            completion.TrySetResult(new BridgeResult(
                request.Direction == PreviewZoomDirection.In ? "in" : "out",
                transition.State.DisplayText,
                root.GetProperty("width").GetDouble(),
                root.GetProperty("height").GetDouble(),
                webView.ZoomFactor,
                root.GetProperty("backgroundSize").GetString() ?? string.Empty,
                initialImageLoadMessageValidated,
                zoomMessageCount,
                wheel,
                sourceRefreshPreservedViewport,
                pngPayload.Size.Width,
                pngPayload.Size.Height,
                pngTopLeftIsTransparent,
                zoomNavigationCount,
                contextMenuDispatch.Equals("false", StringComparison.Ordinal),
                contextMenuParsed,
                copyDispatch.Equals("false", StringComparison.Ordinal),
                copyCommandCount,
                visualSelectionStayedTokenAndRevisionBound,
                englishTextMeasured,
                persianTextMeasured,
                textOverlayAligned,
                textBoundsMatchBrowserSvg,
                persianLtrHitMatchesVisibleText,
                oldMirroredPersianLocationDoesNotHit,
                textMeasurementDidNotModifySource,
                measurementSurfaceWasRemoved,
                invalidTextMeasurementRequestsWereIgnored,
                fontChangeMeasured,
                resizeMessagesPassedStrictParser,
                resizeTemporaryOutlineChanged,
                resizeSourceStayedUnchangedBeforeRelease,
                resizeCommittedExactlyOnce,
                resizeRenderedArtworkUpdated,
                resizeHandleStayedFixedSize,
                resizeModifiersPreservedArbitration,
                resizeDidNotNavigateDuringPointerMove,
                resizeKeptWebViewZoomAtOne,
                arrangeChangedTopmostHitAndUndoRestoredIt,
                opacityCommittedAndUndoRestoredSource,
                stageTwoHostEditsDidNotNavigate,
                stageTwoPreviewStayedReadyAtDocumentZoomOne));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            window?.Close();
            webView?.Dispose();
            await Task.Delay(500);
            TryDeleteTestProfile(userDataFolder);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Background);
        }
    }

    private static async Task<IReadOnlyDictionary<int, SvgVisualBounds>>
        MeasureReferenceTextBoundsAsync(
            CoreWebView2 core,
            IReadOnlyList<SvgVisualTextMeasurementSpec> items)
    {
        string serializedItems = JsonSerializer.Serialize(items.Select(item =>
            new
            {
                index = item.Index,
                text = item.Text,
                x = item.X,
                y = item.Y,
                fontSize = item.FontSize,
                fontFamily = item.FontFamily,
                fontWeight = item.FontWeight,
                fontStyle = item.FontStyle,
                textAnchor = item.TextAnchor,
                direction = item.Direction,
                unicodeBidi = item.UnicodeBidi
            }));
        string script =
            """
            (() => {
              const namespace = 'http://www.w3.org/2000/svg';
              const surface = document.createElementNS(namespace, 'svg');
              surface.style.position = 'fixed';
              surface.style.left = '-100000px';
              surface.style.top = '-100000px';
              surface.style.overflow = 'visible';
              surface.style.opacity = '0';
              document.body.appendChild(surface);
              try {
                const results = __ITEMS__.map(item => {
                  const text = document.createElementNS(namespace, 'text');
                  text.setAttribute('x', String(item.x));
                  text.setAttribute('y', String(item.y));
                  text.setAttribute('font-size', String(item.fontSize));
                  text.setAttribute('font-family', item.fontFamily);
                  text.setAttribute('font-weight', item.fontWeight);
                  text.setAttribute('font-style', item.fontStyle);
                  text.setAttribute('text-anchor', item.textAnchor);
                  text.setAttribute('direction', item.direction);
                  text.setAttribute('unicode-bidi', item.unicodeBidi);
                  text.textContent = item.text;
                  surface.appendChild(text);
                  const bounds = text.getBBox();
                  text.remove();
                  return {
                    index: item.index,
                    left: bounds.x,
                    top: bounds.y,
                    right: bounds.x + bounds.width,
                    bottom: bounds.y + bounds.height
                  };
                });
                return JSON.stringify(results);
              } finally {
                surface.remove();
              }
            })()
            """.Replace(
                "__ITEMS__",
                serializedItems,
                StringComparison.Ordinal);
        string encoded = await core.ExecuteScriptAsync(script);
        string json = JsonSerializer.Deserialize<string>(encoded)
            ?? throw new InvalidOperationException(
                "WebView2 returned no reference text bounds.");
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().ToDictionary(
            item => item.GetProperty("index").GetInt32(),
            item => new SvgVisualBounds(
                item.GetProperty("left").GetDouble(),
                item.GetProperty("top").GetDouble(),
                item.GetProperty("right").GetDouble(),
                item.GetProperty("bottom").GetDouble()));
    }

    private static bool TryReadOverlayBounds(
        string value,
        out SvgVisualBounds bounds)
    {
        bounds = default;
        string[] parts = value.Split('|');
        if (parts.Length != 5
            || !parts[0].Equals("rect", StringComparison.Ordinal)
            || !double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double left)
            || !double.TryParse(
                parts[2],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double top)
            || !double.TryParse(
                parts[3],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double width)
            || !double.TryParse(
                parts[4],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double height))
        {
            return false;
        }

        bounds = new SvgVisualBounds(
            left,
            top,
            left + width,
            top + height);
        return true;
    }

    private static bool BoundsMatch(
        SvgVisualBounds actual,
        SvgVisualBounds expected,
        double tolerance) =>
        NearlyEqual(actual.Left, expected.Left, tolerance)
        && NearlyEqual(actual.Top, expected.Top, tolerance)
        && NearlyEqual(actual.Right, expected.Right, tolerance)
        && NearlyEqual(actual.Bottom, expected.Bottom, tolerance);

    private static async Task<string> ReadVisualSelectionAsync(
        CoreWebView2 core)
    {
        string json = await core.ExecuteScriptAsync(
            """
            (() => {
              const shape = document.querySelector(
                '.selection-overlay > .selection-shape');
              return shape
                ? [
                    shape.localName,
                    shape.getAttribute('x'),
                    shape.getAttribute('y'),
                    shape.getAttribute('width'),
                    shape.getAttribute('height')
                  ].join('|')
                : '';
            })()
            """);
        return JsonSerializer.Deserialize<string>(json) ?? string.Empty;
    }

    private static async Task<ResizeHandleMetrics> GetResizeHandleMetricsAsync(
        CoreWebView2 core,
        string handleId)
    {
        string serializedHandle = JsonSerializer.Serialize(handleId);
        string json = await core.ExecuteScriptAsync(
            $$"""
            JSON.stringify((() => {
              const handle = document.querySelector(
                `.resize-handle[data-handle=${CSS.escape({{serializedHandle}})}]`);
              if (!handle) {
                return null;
              }
              const rect = handle.getBoundingClientRect();
              return {
                centerX: rect.left + rect.width / 2,
                centerY: rect.top + rect.height / 2,
                width: rect.width,
                height: rect.height
              };
            })())
            """);
        string? decoded = JsonSerializer.Deserialize<string>(json);
        return JsonSerializer.Deserialize<ResizeHandleMetrics>(
            decoded ?? "null",
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException(
                $"Resize handle {handleId} was not rendered.");
    }

    private static double ParseScriptNumber(string json)
    {
        return JsonSerializer.Deserialize<double>(json);
    }

    private sealed record ViewportMetrics(
        double Left,
        double Top,
        double ClientWidth,
        double ClientHeight,
        double MaxLeft,
        double MaxTop);

    private sealed record ResizeHandleMetrics(
        double CenterX,
        double CenterY,
        double Width,
        double Height);

    private static async Task<WheelResult> RunWheelInputChecksAsync(
        CoreWebView2 core,
        Func<int> getZoomMessageCount)
    {
        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeHorizontal = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: 80, deltaY: 0);
        ViewportMetrics afterHorizontal = await GetViewportMetricsAsync(core);
        bool horizontalPositiveScrolled =
            afterHorizontal.Left > beforeHorizontal.Left;
        bool horizontalPreservedVertical =
            NearlyEqual(afterHorizontal.Top, beforeHorizontal.Top);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeNegative = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: -40, deltaY: 0);
        ViewportMetrics afterNegative = await GetViewportMetricsAsync(core);
        bool horizontalNegativeScrolled =
            afterNegative.Left < beforeNegative.Left;

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeFractional = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: 0.75, deltaY: 0);
        ViewportMetrics afterFractional = await GetViewportMetricsAsync(core);
        bool fractionalHorizontalPreserved = NearlyEqual(
            afterFractional.Left - beforeFractional.Left,
            0.75,
            tolerance: 0.2);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeVertical = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: 0, deltaY: 60);
        ViewportMetrics afterVertical = await GetViewportMetricsAsync(core);
        bool verticalScrolled = afterVertical.Top > beforeVertical.Top;
        bool verticalPreservedHorizontal =
            NearlyEqual(afterVertical.Left, beforeVertical.Left);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeDiagonal = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: 45, deltaY: 35);
        ViewportMetrics afterDiagonal = await GetViewportMetricsAsync(core);
        bool diagonalScrolledBothAxes =
            afterDiagonal.Left > beforeDiagonal.Left
            && afterDiagonal.Top > beforeDiagonal.Top;

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeShiftFallback = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(
            core,
            deltaX: 0,
            deltaY: 50,
            modifiers: 8);
        ViewportMetrics afterShiftFallback = await GetViewportMetricsAsync(core);
        bool shiftFallbackScrolledHorizontally =
            afterShiftFallback.Left > beforeShiftFallback.Left
            && NearlyEqual(afterShiftFallback.Top, beforeShiftFallback.Top);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeNativeShift = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(
            core,
            deltaX: 25,
            deltaY: 80,
            modifiers: 8);
        ViewportMetrics afterNativeShift = await GetViewportMetricsAsync(core);
        bool shiftNativeHorizontalWasNotDoubled =
            NearlyEqual(
                afterNativeShift.Left - beforeNativeShift.Left,
                25,
                tolerance: 1)
            && NearlyEqual(afterNativeShift.Top, beforeNativeShift.Top);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeLine = await GetViewportMetricsAsync(core);
        await DispatchDomWheelAsync(
            core,
            deltaX: 1,
            deltaY: 0,
            deltaMode: 1);
        ViewportMetrics afterLine = await GetViewportMetricsAsync(core);
        bool lineModeNormalized = NearlyEqual(
            afterLine.Left - beforeLine.Left,
            16,
            tolerance: 0.6);

        await SetViewportAsync(core, 0, 100);
        ViewportMetrics beforePage = await GetViewportMetricsAsync(core);
        await DispatchDomWheelAsync(
            core,
            deltaX: 1,
            deltaY: 0,
            deltaMode: 2);
        ViewportMetrics afterPage = await GetViewportMetricsAsync(core);
        bool pageModeNormalized = NearlyEqual(
            afterPage.Left - beforePage.Left,
            beforePage.ClientWidth,
            tolerance: 1.5);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeMalformed = await GetViewportMetricsAsync(core);
        await core.ExecuteScriptAsync(
            """
            window.dispatchEvent(new WheelEvent('wheel', {
              deltaX: Number.NaN,
              deltaY: Number.POSITIVE_INFINITY,
              deltaMode: 99,
              clientX: 200,
              clientY: 150,
              bubbles: true,
              cancelable: true
            }))
            """);
        ViewportMetrics afterMalformed = await GetViewportMetricsAsync(core);
        bool malformedDeltaIgnored =
            NearlyEqual(afterMalformed.Left, beforeMalformed.Left)
            && NearlyEqual(afterMalformed.Top, beforeMalformed.Top);

        await SetViewportAsync(core, 0, 0);
        await DispatchWheelAsync(core, deltaX: -80, deltaY: 0);
        ViewportMetrics atStart = await GetViewportMetricsAsync(core);
        await SetViewportToEndAsync(core);
        ViewportMetrics beforeEndClamp = await GetViewportMetricsAsync(core);
        await DispatchWheelAsync(core, deltaX: 80, deltaY: 80);
        ViewportMetrics atEnd = await GetViewportMetricsAsync(core);
        bool clampedAtBothEnds =
            NearlyEqual(atStart.Left, 0)
            && NearlyEqual(atStart.Top, 0)
            && beforeEndClamp.Left > 0
            && beforeEndClamp.Top > 0
            && NearlyEqual(atEnd.Left, beforeEndClamp.Left)
            && NearlyEqual(atEnd.Top, beforeEndClamp.Top);

        PreviewPageMessageBuilder pageMessageBuilder = new();
        core.PostWebMessageAsJson(
            pageMessageBuilder.BuildZoomStateMessage(
                BridgeToken,
                100,
                50,
                PreviewViewportPosition.Center));
        await Task.Delay(100);
        await SetViewportAsync(core, 0, 0);
        await DispatchWheelAsync(core, deltaX: 80, deltaY: 0);
        ViewportMetrics withoutOverflow = await GetViewportMetricsAsync(core);
        bool noOverflowDidNotMove =
            NearlyEqual(withoutOverflow.MaxLeft, 0)
            && NearlyEqual(withoutOverflow.Left, 0);

        core.PostWebMessageAsJson(
            pageMessageBuilder.BuildZoomStateMessage(
                BridgeToken,
                1800,
                900,
                PreviewViewportPosition.Center));
        await Task.Delay(100);

        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeNativeHost = await GetViewportMetricsAsync(core);
        core.PostWebMessageAsJson(
            pageMessageBuilder.BuildHorizontalScrollMessage(
                BridgeToken,
                deltaX: 40.5));
        await Task.Delay(50);
        ViewportMetrics afterNativeHost = await GetViewportMetricsAsync(core);
        bool nativeHostMessageScrolledOnce = NearlyEqual(
            afterNativeHost.Left - beforeNativeHost.Left,
            40.5,
            tolerance: 0.6);
        bool nativeHostMessagePreservedVertical = NearlyEqual(
            afterNativeHost.Top,
            beforeNativeHost.Top);

        string staleToken = "FFEEDDCCBBAA99887766554433221100";
        core.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "horizontalScroll",
            token = staleToken,
            deltaX = 40.5
        }));
        core.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "horizontalScroll",
            token = BridgeToken,
            deltaX = 40.5,
            extra = true
        }));
        await Task.Delay(50);
        ViewportMetrics afterRejectedNativeHost =
            await GetViewportMetricsAsync(core);
        bool staleAndExtraNativeMessagesIgnored =
            NearlyEqual(
                afterRejectedNativeHost.Left,
                afterNativeHost.Left)
            && NearlyEqual(
                afterRejectedNativeHost.Top,
                afterNativeHost.Top);

        bool ordinaryWheelDidNotZoom = getZoomMessageCount() == 0;
        await SetViewportAsync(core, 100, 100);
        ViewportMetrics beforeCtrl = await GetViewportMetricsAsync(core);
        await core.ExecuteScriptAsync(
            """
            window.__svgLiveEditorCtrlWheelCanceled = false;
            window.addEventListener(
              'wheel',
              event => {
                window.__svgLiveEditorCtrlWheelCanceled =
                  event.defaultPrevented;
              },
              { once: true });
            """);
        await DispatchWheelAsync(
            core,
            deltaX: 0,
            deltaY: -120,
            modifiers: 2);
        ViewportMetrics afterCtrl = await GetViewportMetricsAsync(core);
        bool ctrlWheelCanceled = JsonSerializer.Deserialize<bool>(
            await core.ExecuteScriptAsync(
                "window.__svgLiveEditorCtrlWheelCanceled"));
        bool ctrlWheelDidNotScroll =
            NearlyEqual(afterCtrl.Left, beforeCtrl.Left)
            && NearlyEqual(afterCtrl.Top, beforeCtrl.Top);

        return new WheelResult(
            horizontalPositiveScrolled,
            horizontalNegativeScrolled,
            horizontalPreservedVertical,
            fractionalHorizontalPreserved,
            clampedAtBothEnds,
            noOverflowDidNotMove,
            verticalScrolled,
            verticalPreservedHorizontal,
            diagonalScrolledBothAxes,
            shiftFallbackScrolledHorizontally,
            shiftNativeHorizontalWasNotDoubled,
            lineModeNormalized,
            pageModeNormalized,
            malformedDeltaIgnored,
            nativeHostMessageScrolledOnce,
            nativeHostMessagePreservedVertical,
            staleAndExtraNativeMessagesIgnored,
            ctrlWheelCanceled,
            ctrlWheelDidNotScroll,
            ordinaryWheelDidNotZoom);
    }

    private static async Task DispatchWheelAsync(
        CoreWebView2 core,
        double deltaX,
        double deltaY,
        int modifiers = 0)
    {
        string parameters = JsonSerializer.Serialize(new
        {
            type = "mouseWheel",
            x = 200,
            y = 150,
            deltaX,
            deltaY,
            modifiers,
            pointerType = "mouse"
        });
        await core.CallDevToolsProtocolMethodAsync(
            "Input.dispatchMouseEvent",
            parameters);
        await Task.Delay(50);
    }

    private static async Task DispatchMouseAsync(
        CoreWebView2 core,
        string type,
        double x,
        double y,
        int buttons,
        string button,
        int modifiers = 0)
    {
        string parameters = JsonSerializer.Serialize(new
        {
            type,
            x,
            y,
            button,
            buttons,
            modifiers,
            clickCount = type == "mousePressed" ? 1 : 0,
            pointerType = "mouse"
        });
        await core.CallDevToolsProtocolMethodAsync(
            "Input.dispatchMouseEvent",
            parameters);
        await Task.Delay(50);
    }

    private static async Task DispatchDomWheelAsync(
        CoreWebView2 core,
        double deltaX,
        double deltaY,
        int deltaMode)
    {
        string parameters = JsonSerializer.Serialize(new
        {
            deltaX,
            deltaY,
            deltaMode
        });
        await core.ExecuteScriptAsync(
            $$"""
            (() => {
              const input = {{parameters}};
              window.dispatchEvent(new WheelEvent('wheel', {
                deltaX: input.deltaX,
                deltaY: input.deltaY,
                deltaMode: input.deltaMode,
                clientX: 200,
                clientY: 150,
                bubbles: true,
                cancelable: true
              }));
            })()
            """);
        await Task.Delay(20);
    }

    private static async Task SetViewportAsync(
        CoreWebView2 core,
        double left,
        double top)
    {
        string parameters = JsonSerializer.Serialize(new
        {
            left,
            top
        });
        await core.ExecuteScriptAsync(
            $$"""
            (() => {
              const point = {{parameters}};
              const viewport = document.querySelector('.preview-viewport');
              viewport.scrollLeft = point.left;
              viewport.scrollTop = point.top;
            })()
            """);
        await Task.Delay(20);
    }

    private static async Task SetViewportToEndAsync(CoreWebView2 core)
    {
        await core.ExecuteScriptAsync(
            """
            (() => {
              const viewport = document.querySelector('.preview-viewport');
              viewport.scrollLeft = viewport.scrollWidth;
              viewport.scrollTop = viewport.scrollHeight;
            })()
            """);
        await Task.Delay(20);
    }

    private static async Task<ViewportMetrics> GetViewportMetricsAsync(
        CoreWebView2 core)
    {
        string result = await core.ExecuteScriptAsync(
            """
            JSON.stringify((() => {
              const viewport = document.querySelector('.preview-viewport');
              return {
                left: viewport.scrollLeft,
                top: viewport.scrollTop,
                clientWidth: viewport.clientWidth,
                clientHeight: viewport.clientHeight,
                maxLeft: Math.max(0, viewport.scrollWidth - viewport.clientWidth),
                maxTop: Math.max(0, viewport.scrollHeight - viewport.clientHeight)
              };
            })())
            """);
        string json = JsonSerializer.Deserialize<string>(result)
            ?? throw new InvalidOperationException(
                "WebView2 returned no viewport metrics.");
        return JsonSerializer.Deserialize<ViewportMetrics>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidOperationException(
                "WebView2 returned invalid viewport metrics.");
    }

    private static bool NearlyEqual(
        double first,
        double second,
        double tolerance = 0.01)
    {
        return Math.Abs(first - second) <= tolerance;
    }

    private static bool IsTopLeftTransparent(byte[] pngBytes)
    {
        using MemoryStream stream = new(pngBytes, writable: false);
        BitmapFrame frame = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad).Frames[0];
        FormatConvertedBitmap bitmap = new(
            frame,
            PixelFormats.Bgra32,
            null,
            0);
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(
            new Int32Rect(0, 0, 1, 1),
            pixel,
            stride: 4,
            offset: 0);
        return pixel[3] == 0;
    }

    private static async Task NavigateAsync(CoreWebView2 core, string html)
    {
        TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs> completed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args) =>
            completed.TrySetResult(args);

        core.NavigationCompleted += OnCompleted;
        try
        {
            core.NavigateToString(html);
            CoreWebView2NavigationCompletedEventArgs result =
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"WebView2 navigation failed: {result.WebErrorStatus}");
            }
        }
        finally
        {
            core.NavigationCompleted -= OnCompleted;
        }
    }

    private static void TryDeleteTestProfile(string userDataFolder)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(userDataFolder))
                {
                    Directory.Delete(userDataFolder, recursive: true);
                }
                return;
            }
            catch (IOException)
            {
                if (attempt < 4)
                {
                    Thread.Sleep(100);
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt < 4)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
