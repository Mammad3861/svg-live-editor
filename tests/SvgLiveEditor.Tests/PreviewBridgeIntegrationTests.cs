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
        string BackgroundSize);

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task TrustedPage_CdpCtrlWheel_TraversesBridgeAndUpdatesOnlyImage()
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
            await NavigateAsync(core, htmlBuilder.Build(svg, 300, 150, BridgeToken));

            TaskCompletionSource<(string Source, string Json)> messageReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            core.WebMessageReceived += (_, args) =>
                messageReceived.TrySetResult((args.Source, args.WebMessageAsJson));

            await core.CallDevToolsProtocolMethodAsync(
                "Input.dispatchMouseEvent",
                """
                {
                  "type": "mouseWheel",
                  "x": 320,
                  "y": 240,
                  "deltaX": 0,
                  "deltaY": -120,
                  "modifiers": 2,
                  "pointerType": "mouse"
                }
                """);

            (string source, string json) =
                await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
            await NavigateAsync(
                core,
                htmlBuilder.Build(
                    svg,
                    transition.RenderedWidth,
                    transition.RenderedHeight,
                    BridgeToken,
                    transition.Scroll));

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
                root.GetProperty("backgroundSize").GetString() ?? string.Empty));
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
