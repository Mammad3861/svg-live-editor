using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PreviewGeometryPolishIntegrationTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";

    public TestContext TestContext { get; set; } = null!;

    private sealed record BidiCase(
        string Name,
        string Text,
        string Direction,
        string UnicodeBidi,
        string TextAnchor,
        string FontFamily);

    private sealed record TextEvidence(
        string Name,
        SvgVisualBounds Measurement,
        SvgVisualBounds Pixels,
        SvgVisualBounds Overlay);

    private sealed record SurfaceEvidence(
        double ImageWidth,
        double ImageHeight,
        double StageWidth,
        double StageHeight,
        double ScrollWidth,
        double ScrollHeight,
        double ClientWidth,
        double ClientHeight,
        bool TopLeftReachable,
        bool BottomRightReachable,
        bool SurfaceClamped,
        string BackgroundSize,
        double WebViewZoomFactor,
        bool HadDefaultWpfFocusVisual,
        bool AppOwnedFocusVisualRemoved,
        bool PointerFocusHasNoDomShadow,
        bool FocusLossClearsPointerClass,
        bool ExtremeSurfaceWasCapped);

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task TrustedPage_BidiPixelsAndOutsideCanvasHandlesStayAligned()
    {
        TaskCompletionSource<(IReadOnlyList<TextEvidence> Text,
            SurfaceEvidence Surface, int MeasurementCount)>
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));
            _ = RunHarnessAsync(completion);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        (IReadOnlyList<TextEvidence> text, SurfaceEvidence surface,
            int measurementCount) = await completion.Task.WaitAsync(
            TimeSpan.FromSeconds(30));

        Assert.HasCount(8, text);
        Assert.AreEqual(text.Count, measurementCount,
            "A stale-revision measurement request must not produce a result.");
        foreach (TextEvidence evidence in text)
        {
            TestContext.WriteLine(
                $"{evidence.Name}: measurement={evidence.Measurement}; pixels={evidence.Pixels}; overlay={evidence.Overlay}");
            AssertBoundsMatch(
                evidence.Measurement,
                evidence.Overlay,
                0.5,
                $"{evidence.Name} overlay");
            AssertInkInsideMeasurement(evidence);
        }

        TextEvidence persianLtr = text.Single(item => item.Name == "persian-ltr");
        TextEvidence persianRtl = text.Single(item => item.Name == "persian-rtl");
        double measuredShift = CenterX(persianRtl.Measurement)
            - CenterX(persianLtr.Measurement);
        double pixelShift = CenterX(persianRtl.Pixels)
            - CenterX(persianLtr.Pixels);
        Assert.IsTrue(Math.Abs(measuredShift) > 20,
            "Changing direction with text-anchor=start should visibly change the anchor side.");
        Assert.AreEqual(Math.Sign(pixelShift), Math.Sign(measuredShift),
            "The measured RTL/LTR shift must follow the rendered pixels.");

        Assert.AreEqual(640, surface.ImageWidth, 0.5);
        Assert.AreEqual(180, surface.ImageHeight, 0.5);
        Assert.IsTrue(surface.StageWidth > surface.ImageWidth);
        Assert.IsTrue(surface.StageHeight > surface.ImageHeight);
        Assert.IsTrue(surface.StageWidth <= 100_000);
        Assert.IsTrue(surface.StageHeight <= 100_000);
        Assert.IsTrue(surface.ScrollWidth > surface.ClientWidth);
        Assert.IsTrue(surface.ScrollHeight > surface.ClientHeight);
        Assert.IsTrue(surface.TopLeftReachable);
        Assert.IsTrue(surface.BottomRightReachable);
        Assert.IsFalse(surface.SurfaceClamped);
        Assert.AreEqual(
            "24px 24px, 24px 24px, 24px 24px, 24px 24px",
            surface.BackgroundSize);
        Assert.AreEqual(1.0, surface.WebViewZoomFactor, 0.0001);
        Assert.IsTrue(surface.HadDefaultWpfFocusVisual);
        Assert.IsTrue(surface.AppOwnedFocusVisualRemoved);
        Assert.IsTrue(surface.PointerFocusHasNoDomShadow);
        Assert.IsTrue(surface.FocusLossClearsPointerClass);
        Assert.IsTrue(surface.ExtremeSurfaceWasCapped);
        TestContext.WriteLine($"interaction surface: {surface}");
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<(IReadOnlyList<TextEvidence> Text,
            SurfaceEvidence Surface, int MeasurementCount)> completion)
    {
        string userDataFolder = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.Tests",
            Guid.NewGuid().ToString("N"));
        Window? window = null;
        WebView2CompositionControl? webView = null;
        try
        {
            window = new Window
            {
                Title = "SvgLiveEditor geometry polish integration",
                Width = 420,
                Height = 320,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow
            };
            webView = new WebView2CompositionControl();
            window.Content = webView;
            window.Show();
            bool hadDefaultWpfFocusVisual = webView.GetValue(
                FrameworkElement.FocusVisualStyleProperty) is not null;
            webView.SetValue(FrameworkElement.FocusVisualStyleProperty, null);
            bool appOwnedFocusVisualRemoved = webView.GetValue(
                FrameworkElement.FocusVisualStyleProperty) is null;

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);
            CoreWebView2 core = webView.CoreWebView2
                ?? throw new InvalidOperationException(
                    "WebView2 did not initialize.");
            core.Settings.IsScriptEnabled = true;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            webView.ZoomFactor = 1.0;

            TaskCompletionSource<string>? imageWaiter = null;
            TaskCompletionSource<string>? measurementWaiter = null;
            int measurementCount = 0;
            core.WebMessageReceived += (_, args) =>
            {
                using JsonDocument message =
                    JsonDocument.Parse(args.WebMessageAsJson);
                string? type = message.RootElement.TryGetProperty(
                        "type",
                        out JsonElement typeElement)
                    ? typeElement.GetString()
                    : null;
                if (type == "imageState")
                {
                    imageWaiter?.TrySetResult(args.WebMessageAsJson);
                }
                else if (type == "textMeasurements")
                {
                    measurementCount++;
                    measurementWaiter?.TrySetResult(args.WebMessageAsJson);
                }
            };

            BidiCase[] cases =
            [
                new("persian-ltr", "بهروز", "ltr", "plaintext", "start",
                    "\"Segoe UI\", sans-serif"),
                new("persian-rtl", "بهروز", "rtl", "plaintext", "start",
                    "\"Segoe UI\", sans-serif"),
                new("mixed-ltr", "بهروز SVG 123!", "ltr", "plaintext", "start",
                    "Tahoma, sans-serif"),
                new("mixed-rtl", "SVG بهروز ۱۲۳!", "rtl", "plaintext", "start",
                    "Tahoma, sans-serif"),
                new("digits-embed", "قیمت ۱۲۳٬۴۵۶ تومان", "rtl", "embed", "middle",
                    "Tahoma, sans-serif"),
                new("punctuation-normal", "(آزمون)؟! 42", "rtl", "normal", "end",
                    "Tahoma, sans-serif"),
                new("latin-persian-middle", "Hello — سلام!", "ltr", "normal", "middle",
                    "Segoe UI, sans-serif"),
                new("fallback-end", "بهروز fallback", "rtl", "plaintext", "end",
                    "\"SvgLiveEditor Missing Font\", \"Segoe UI\", sans-serif")
            ];
            PreviewHtmlBuilder htmlBuilder = new();
            PreviewPageMessageBuilder messageBuilder = new();
            SvgVisualViewport viewport = new(
                0,
                0,
                640,
                180,
                SvgPreserveAspectRatio.Default);
            List<TextEvidence> evidence = [];
            long revision = 1;

            for (int index = 0; index < cases.Length; index++)
            {
                BidiCase testCase = cases[index];
                string source = BuildTextSvg(testCase);
                imageWaiter = new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                if (index == 0)
                {
                    await NavigateAsync(
                        core,
                        htmlBuilder.Build(
                            source,
                            640,
                            180,
                            BridgeToken,
                            PreviewViewportPosition.Center,
                            revision,
                            viewport));
                }
                else
                {
                    core.PostWebMessageAsJson(
                        messageBuilder.BuildRenderImageMessage(
                            BridgeToken,
                            revision,
                            source,
                            640,
                            180,
                            PreviewViewportPosition.Center));
                }
                await imageWaiter.Task.WaitAsync(TimeSpan.FromSeconds(5));

                SvgVisualTextMeasurementSpec spec = CreateMeasurement(source);
                string requestId = revision.ToString(
                    "X32",
                    CultureInfo.InvariantCulture);
                measurementWaiter = new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                core.PostWebMessageAsJson(
                    messageBuilder.BuildTextMeasurementMessage(
                        BridgeToken,
                        revision,
                        requestId,
                        [spec]));
                string measurementJson =
                    await measurementWaiter.Task.WaitAsync(
                        TimeSpan.FromSeconds(5));
                Assert.IsTrue(new PreviewTextMeasurementMessageParser().TryParse(
                    measurementJson,
                    new PendingPreviewTextMeasurement(
                        BridgeToken,
                        revision,
                        requestId,
                        [spec.Index]),
                    out IReadOnlyList<SvgVisualTextMeasurementResult> results));
                SvgVisualBounds measured = results.Single().Bounds
                    ?? throw new InvalidOperationException(
                        $"No bounds were returned for {testCase.Name}.");

                core.PostWebMessageAsJson(
                    messageBuilder.BuildVisualSelectionMessage(
                        BridgeToken,
                        revision,
                        new PreviewVisualSelection(
                            SvgVisualElementKind.Text,
                            new SvgVisualShapeGeometry(
                                SvgVisualElementKind.Text,
                                measured.Left,
                                measured.Top,
                                measured.Right,
                                measured.Bottom),
                            0,
                            0,
                            "0123456789ABCDEFFEDCBA9876543210",
                            [])));
                await WaitForAnimationFramesAsync(core);
                SvgVisualBounds pixels = await ReadImagePixelBoundsAsync(core);
                SvgVisualBounds overlay = await ReadOverlayBoundsAsync(core);
                evidence.Add(new TextEvidence(
                    testCase.Name,
                    measured,
                    pixels,
                    overlay));
                revision++;
            }

            int beforeStale = measurementCount;
            SvgVisualTextMeasurementSpec currentSpec =
                CreateMeasurement(BuildTextSvg(cases[^1]));
            core.PostWebMessageAsJson(
                messageBuilder.BuildTextMeasurementMessage(
                    BridgeToken,
                    revision - 2,
                    "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                    [currentSpec]));
            await Task.Delay(100);
            if (measurementCount != beforeStale)
            {
                throw new InvalidOperationException(
                    "The trusted page accepted a stale text-measurement revision.");
            }

            core.PostWebMessageAsJson(
                messageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    revision - 1,
                    new PreviewVisualSelection(
                        SvgVisualElementKind.Rect,
                        new SvgVisualShapeGeometry(
                            SvgVisualElementKind.Rect,
                            -80,
                            -40,
                            720,
                            220),
                        0,
                        0,
                        "1123456789ABCDEFFEDCBA9876543210",
                        [
                            new SvgResizeHandleDefinition(
                                SvgResizeHandle.TopLeft,
                                new SvgVisualPoint(-80, -40)),
                            new SvgResizeHandleDefinition(
                                SvgResizeHandle.BottomRight,
                                new SvgVisualPoint(720, 220))
                        ])));
            await WaitForAnimationFramesAsync(core);
            SurfaceEvidence surface = await ReadSurfaceEvidenceAsync(
                core,
                webView.ZoomFactor,
                hadDefaultWpfFocusVisual,
                appOwnedFocusVisualRemoved);
            core.PostWebMessageAsJson(
                messageBuilder.BuildVisualSelectionMessage(
                    BridgeToken,
                    revision - 1,
                    new PreviewVisualSelection(
                        SvgVisualElementKind.Rect,
                        new SvgVisualShapeGeometry(
                            SvgVisualElementKind.Rect,
                            -999_999_999,
                            -999_999_999,
                            999_999_999,
                            999_999_999),
                        0,
                        0,
                        "2123456789ABCDEFFEDCBA9876543210",
                        [])));
            await WaitForAnimationFramesAsync(core);
            surface = surface with
            {
                ExtremeSurfaceWasCapped =
                    await ReadExtremeSurfaceWasCappedAsync(core)
            };

            completion.TrySetResult((evidence, surface, measurementCount));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            window?.Close();
            webView?.Dispose();
            await Task.Delay(250);
            TryDeleteTestProfile(userDataFolder);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Background);
        }
    }

    private static string BuildTextSvg(BidiCase testCase)
    {
        string escapedText = System.Security.SecurityElement.Escape(
            testCase.Text)
            ?? throw new InvalidOperationException("Text escaping failed.");
        string escapedFont = System.Security.SecurityElement.Escape(
            testCase.FontFamily)
            ?? throw new InvalidOperationException("Font escaping failed.");
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 640 180\"><text id=\"label\" x=\"320\" y=\"110\" font-size=\"36\" font-family=\"{escapedFont}\" font-weight=\"400\" font-style=\"normal\" text-anchor=\"{testCase.TextAnchor}\" direction=\"{testCase.Direction}\" unicode-bidi=\"{testCase.UnicodeBidi}\">{escapedText}</text></svg>";
    }

    private static SvgVisualTextMeasurementSpec CreateMeasurement(string source)
    {
        SvgDocumentIndexResult index = new SvgDocumentIndexService().Build(source);
        if (!index.IsIndexed || index.Document is null)
        {
            throw new InvalidOperationException(index.IndexError);
        }
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            index.Document,
            new SvgCanvasSizeReader().Read(source),
            source);
        return visual.Elements.Single().TextMeasurement
            ?? throw new InvalidOperationException(
                visual.Elements.Single().UnsupportedReason);
    }

    private static async Task<SvgVisualBounds> ReadImagePixelBoundsAsync(
        CoreWebView2 core)
    {
        string encoded = await core.ExecuteScriptAsync(
            """
            JSON.stringify((() => {
              const image = document.querySelector('img');
              const overlay = document.querySelector('.selection-overlay');
              const viewBox = overlay.viewBox.baseVal;
              const canvas = document.createElement('canvas');
              canvas.width = Math.ceil(viewBox.width);
              canvas.height = Math.ceil(viewBox.height);
              const context = canvas.getContext('2d', { alpha: true });
              context.clearRect(0, 0, canvas.width, canvas.height);
              context.drawImage(image, 0, 0);
              const pixels = context.getImageData(
                0, 0, canvas.width, canvas.height).data;
              let left = canvas.width;
              let top = canvas.height;
              let right = -1;
              let bottom = -1;
              for (let y = 0; y < canvas.height; y++) {
                for (let x = 0; x < canvas.width; x++) {
                  if (pixels[((y * canvas.width + x) * 4) + 3] <= 8) {
                    continue;
                  }
                  left = Math.min(left, x);
                  top = Math.min(top, y);
                  right = Math.max(right, x + 1);
                  bottom = Math.max(bottom, y + 1);
                }
              }
              return right > left && bottom > top
                ? { left, top, right, bottom }
                : null;
            })())
            """);
        return ParseBounds(encoded, "rendered text pixels");
    }

    private static async Task<SvgVisualBounds> ReadOverlayBoundsAsync(
        CoreWebView2 core)
    {
        string encoded = await core.ExecuteScriptAsync(
            """
            JSON.stringify((() => {
              const image = document.querySelector('img');
              const overlay = document.querySelector('.selection-overlay');
              const shape = document.querySelector(
                '.selection-overlay > .selection-accent');
              if (!image || !overlay || !shape) {
                return null;
              }
              const imageRect = image.getBoundingClientRect();
              const shapeRect = shape.getBoundingClientRect();
              const viewBox = overlay.viewBox.baseVal;
              const scaleX = viewBox.width / imageRect.width;
              const scaleY = viewBox.height / imageRect.height;
              return {
                left: viewBox.x +
                  ((shapeRect.left - imageRect.left) * scaleX),
                top: viewBox.y +
                  ((shapeRect.top - imageRect.top) * scaleY),
                right: viewBox.x +
                  ((shapeRect.right - imageRect.left) * scaleX),
                bottom: viewBox.y +
                  ((shapeRect.bottom - imageRect.top) * scaleY)
              };
            })())
            """);
        return ParseBounds(encoded, "selection overlay");
    }

    private static async Task<SurfaceEvidence> ReadSurfaceEvidenceAsync(
        CoreWebView2 core,
        double zoomFactor,
        bool hadDefaultWpfFocusVisual,
        bool appOwnedFocusVisualRemoved)
    {
        string encoded = await core.ExecuteScriptAsync(
            """
            JSON.stringify((() => {
              const viewport = document.querySelector('.preview-viewport');
              const stage = document.querySelector('main');
              const image = document.querySelector('img');
              const topLeft = document.querySelector(
                '.resize-handle[data-handle="top-left"]');
              const bottomRight = document.querySelector(
                '.resize-handle[data-handle="bottom-right"]');
              const viewportRect = viewport.getBoundingClientRect();
              viewport.scrollLeft = 0;
              viewport.scrollTop = 0;
              const topLeftRect = topLeft.getBoundingClientRect();
              const topLeftReachable =
                topLeftRect.left >= viewportRect.left &&
                topLeftRect.right <= viewportRect.right &&
                topLeftRect.top >= viewportRect.top &&
                topLeftRect.bottom <= viewportRect.bottom;
              viewport.scrollLeft = viewport.scrollWidth;
              viewport.scrollTop = viewport.scrollHeight;
              const bottomRightRect = bottomRight.getBoundingClientRect();
              const bottomRightReachable =
                bottomRightRect.left >= viewportRect.left &&
                bottomRightRect.right <= viewportRect.right &&
                bottomRightRect.top >= viewportRect.top &&
                bottomRightRect.bottom <= viewportRect.bottom;
              const imageRect = image.getBoundingClientRect();
              const stageRect = stage.getBoundingClientRect();
              viewport.dispatchEvent(new PointerEvent('pointerdown', {
                bubbles: true,
                button: 0,
                buttons: 1,
                pointerId: 41,
                pointerType: 'mouse',
                isPrimary: true
              }));
              viewport.focus({ preventScroll: true });
              const pointerFocusHasNoDomShadow =
                getComputedStyle(viewport).boxShadow === 'none';
              viewport.dispatchEvent(new FocusEvent('blur'));
              const focusLossClearsPointerClass =
                !viewport.classList.contains('pointer-focused');
              return {
                imageWidth: imageRect.width,
                imageHeight: imageRect.height,
                stageWidth: stageRect.width,
                stageHeight: stageRect.height,
                scrollWidth: viewport.scrollWidth,
                scrollHeight: viewport.scrollHeight,
                clientWidth: viewport.clientWidth,
                clientHeight: viewport.clientHeight,
                topLeftReachable,
                bottomRightReachable,
                surfaceClamped:
                  stage.dataset.interactionSurfaceClamped === 'true',
                backgroundSize:
                  getComputedStyle(document.body).backgroundSize,
                pointerFocusHasNoDomShadow,
                focusLossClearsPointerClass
              };
            })())
            """);
        string json = JsonSerializer.Deserialize<string>(encoded)
            ?? throw new InvalidOperationException(
                "WebView2 returned no interaction-surface evidence.");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new SurfaceEvidence(
            root.GetProperty("imageWidth").GetDouble(),
            root.GetProperty("imageHeight").GetDouble(),
            root.GetProperty("stageWidth").GetDouble(),
            root.GetProperty("stageHeight").GetDouble(),
            root.GetProperty("scrollWidth").GetDouble(),
            root.GetProperty("scrollHeight").GetDouble(),
            root.GetProperty("clientWidth").GetDouble(),
            root.GetProperty("clientHeight").GetDouble(),
            root.GetProperty("topLeftReachable").GetBoolean(),
            root.GetProperty("bottomRightReachable").GetBoolean(),
            root.GetProperty("surfaceClamped").GetBoolean(),
            root.GetProperty("backgroundSize").GetString() ?? string.Empty,
            zoomFactor,
            hadDefaultWpfFocusVisual,
            appOwnedFocusVisualRemoved,
            root.GetProperty("pointerFocusHasNoDomShadow").GetBoolean(),
            root.GetProperty("focusLossClearsPointerClass").GetBoolean(),
            ExtremeSurfaceWasCapped: false);
    }

    private static async Task<bool> ReadExtremeSurfaceWasCappedAsync(
        CoreWebView2 core)
    {
        string encoded = await core.ExecuteScriptAsync(
            """
            JSON.stringify((() => {
              const stage = document.querySelector('main');
              const rect = stage.getBoundingClientRect();
              return stage.dataset.interactionSurfaceClamped === 'true' &&
                rect.width <= 100000 && rect.height <= 100000;
            })())
            """);
        return JsonSerializer.Deserialize<bool>(
            JsonSerializer.Deserialize<string>(encoded)
                ?? throw new InvalidOperationException(
                    "WebView2 returned no interaction-surface cap evidence."));
    }

    private static SvgVisualBounds ParseBounds(string encoded, string label)
    {
        string json = JsonSerializer.Deserialize<string>(encoded)
            ?? throw new InvalidOperationException(
                $"WebView2 returned no {label} bounds.");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"WebView2 returned invalid {label} bounds: {json}");
        }
        return new SvgVisualBounds(
            root.GetProperty("left").GetDouble(),
            root.GetProperty("top").GetDouble(),
            root.GetProperty("right").GetDouble(),
            root.GetProperty("bottom").GetDouble());
    }

    private static void AssertInkInsideMeasurement(TextEvidence evidence)
    {
        const double tolerance = 2;
        Assert.IsTrue(
            evidence.Pixels.Left >= evidence.Measurement.Left - tolerance &&
            evidence.Pixels.Top >= evidence.Measurement.Top - tolerance &&
            evidence.Pixels.Right <= evidence.Measurement.Right + tolerance &&
            evidence.Pixels.Bottom <= evidence.Measurement.Bottom + tolerance,
            $"{evidence.Name}: pixels {evidence.Pixels} were outside measured bounds {evidence.Measurement}.");
        Assert.AreEqual(
            CenterX(evidence.Pixels),
            CenterX(evidence.Measurement),
            Math.Max(4, evidence.Measurement.Width * 0.12),
            $"{evidence.Name}: horizontal pixel/measurement centers drifted.");
    }

    private static void AssertBoundsMatch(
        SvgVisualBounds expected,
        SvgVisualBounds actual,
        double tolerance,
        string label)
    {
        Assert.AreEqual(expected.Left, actual.Left, tolerance, label);
        Assert.AreEqual(expected.Top, actual.Top, tolerance, label);
        Assert.AreEqual(expected.Right, actual.Right, tolerance, label);
        Assert.AreEqual(expected.Bottom, actual.Bottom, tolerance, label);
    }

    private static double CenterX(SvgVisualBounds bounds) =>
        (bounds.Left + bounds.Right) / 2;

    private static async Task WaitForAnimationFramesAsync(CoreWebView2 core)
    {
        await core.ExecuteScriptAsync(
            "new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }

    private static async Task NavigateAsync(CoreWebView2 core, string html)
    {
        TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args) =>
            completion.TrySetResult(args);
        core.NavigationCompleted += OnCompleted;
        try
        {
            core.NavigateToString(html);
            CoreWebView2NavigationCompletedEventArgs result =
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
        try
        {
            if (Directory.Exists(userDataFolder))
            {
                Directory.Delete(userDataFolder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
