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
public sealed class PreviewBridgeIntegrationTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private sealed record BridgeResult(
        string Direction,
        string DisplayText,
        double ImageWidth,
        double ImageHeight,
        double WebViewZoomFactor,
        string BackgroundSize,
        int ZoomMessageCount,
        bool CtrlWheelCanceled,
        bool ShiftWheelScrolled,
        bool SourceRefreshPreservedViewport);

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task TrustedPage_CtrlWheelDomHandler_TraversesBridgeOnceAndUpdatesOnlyImage()
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
        Assert.AreEqual(1, result.ZoomMessageCount);
        Assert.IsTrue(result.CtrlWheelCanceled);
        Assert.IsTrue(result.ShiftWheelScrolled);
        Assert.IsTrue(result.SourceRefreshPreservedViewport);
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

            PreviewHtmlBuilder htmlBuilder = new();
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
                    preservedViewport));
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
                    preservedViewport));
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

            TaskCompletionSource<(string Source, string Json)> messageReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int zoomMessageCount = 0;
            core.WebMessageReceived += (_, args) =>
            {
                using JsonDocument message = JsonDocument.Parse(args.WebMessageAsJson);
                if (message.RootElement.TryGetProperty("type", out JsonElement type)
                    && type.GetString() == "zoom")
                {
                    zoomMessageCount++;
                    messageReceived.TrySetResult((args.Source, args.WebMessageAsJson));
                }
            };

            await core.ExecuteScriptAsync(
                """
                window.dispatchEvent(new WheelEvent('wheel', {
                  deltaY: 120,
                  clientX: 320,
                  clientY: 240,
                  bubbles: true,
                  cancelable: true
                }))
                """);
            await Task.Delay(50);
            if (zoomMessageCount != 0)
            {
                throw new InvalidOperationException(
                    "A normal wheel event unexpectedly requested preview zoom.");
            }

            double scrollBeforeShift = ParseScriptNumber(await core.ExecuteScriptAsync(
                "document.querySelector('.preview-viewport').scrollLeft"));
            await core.ExecuteScriptAsync(
                """
                window.dispatchEvent(new WheelEvent('wheel', {
                  deltaY: 120,
                  shiftKey: true,
                  clientX: 320,
                  clientY: 240,
                  bubbles: true,
                  cancelable: true
                }))
                """);
            double scrollAfterShift = ParseScriptNumber(await core.ExecuteScriptAsync(
                "document.querySelector('.preview-viewport').scrollLeft"));

            string dispatchResult = await core.ExecuteScriptAsync(
                """
                window.dispatchEvent(new WheelEvent('wheel', {
                  deltaY: -120,
                  ctrlKey: true,
                  clientX: 320,
                  clientY: 240,
                  bubbles: true,
                  cancelable: true
                }))
                """);

            (string source, string json) =
                await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(150);
            PreviewNavigationPolicy navigationPolicy = new();
            if (!navigationPolicy.IsTrustedWebMessageSource(source))
            {
                throw new InvalidOperationException(
                    $"Unexpected WebMessage source: {source}; current document: {core.Source}");
            }

            PreviewInteractionMessageParser parser = new();
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
            await NavigateAsync(
                core,
                htmlBuilder.Build(
                    svg,
                    transition.RenderedWidth,
                    transition.RenderedHeight,
                    BridgeToken,
                    viewport));

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

            completion.TrySetResult(new BridgeResult(
                request.Direction == PreviewZoomDirection.In ? "in" : "out",
                transition.State.DisplayText,
                root.GetProperty("width").GetDouble(),
                root.GetProperty("height").GetDouble(),
                webView.ZoomFactor,
                root.GetProperty("backgroundSize").GetString() ?? string.Empty,
                zoomMessageCount,
                dispatchResult.Equals("false", StringComparison.Ordinal),
                scrollAfterShift > scrollBeforeShift,
                sourceRefreshPreservedViewport));
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

    private static double ParseScriptNumber(string json)
    {
        return JsonSerializer.Deserialize<double>(json);
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
