using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PreviewPresentationIntegrationTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";

    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task AuthoringLifecycle_ReadyAlwaysAttestsCurrentTrustedPaintedPage()
    {
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
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

        Assert.IsTrue(
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunHarnessAsync(
        TaskCompletionSource<bool> completion)
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
                Width = 640,
                Height = 480,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow
            };
            webView = new WebView2CompositionControl();
            window.Content = webView;
            window.Show();

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);
            CoreWebView2 core = webView.CoreWebView2
                ?? throw new InvalidOperationException(
                    "WebView2 did not initialize.");
            core.Settings.IsScriptEnabled = true;
            core.Settings.IsWebMessageEnabled = true;
            webView.ZoomFactor = 1.0;

            PreviewHtmlBuilder builder = new();
            string welcome = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "samples", "welcome.svg"));

            BrowserSnapshot trustedBeforeBlank = await LoadAndCaptureAsync(
                core,
                webView,
                builder,
                welcome,
                sourceRevision: 0,
                BridgeToken);
            WriteSnapshot("trusted-welcome", trustedBeforeBlank);
            AssertReadySnapshot(trustedBeforeBlank, welcome, 0, 0, 0, BridgeToken);

            // The failing browser state is materially different even though
            // both NavigateToString and an empty page expose about:blank.
            // This captures why URL/source checks alone could not detect it.
            await NavigateToBlankAsync(core);
            BrowserSnapshot emptyBlank = await CaptureBrowserSnapshotAsync(
                core,
                webView,
                expectedSvg: welcome,
                expectedToken: BridgeToken,
                hostSourceRevision: 0,
                latestRequestedRenderRevision: 0,
                latestCompletedRenderRevision: 0);
            WriteSnapshot("empty-about-blank", emptyBlank);
            Assert.AreEqual("about:blank", emptyBlank.CoreSource);
            Assert.AreEqual("about:blank", emptyBlank.Dom.Location);
            Assert.IsFalse(emptyBlank.Dom.HostScriptReady);
            Assert.IsFalse(emptyBlank.Dom.CheckerboardExists);
            Assert.IsFalse(emptyBlank.Dom.ImageExists);
            Assert.AreEqual(Visibility.Visible, emptyBlank.WebViewVisibility);

            using BrowserLifecycleHarness harness = new(core, webView, builder);
            long sourceRevision = 1;
            BrowserSnapshot welcomeReady = await harness.QueueAndWaitAsync(
                welcome,
                sourceRevision++);
            AssertReadySnapshot(
                welcomeReady,
                welcome,
                hostSourceRevision: 1,
                welcomeReady.LatestRequestedRenderRevision,
                welcomeReady.LatestCompletedRenderRevision,
                welcomeReady.NavigationToken);

            string rootGroup = CreateElement(
                welcome,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Group,
                out SvgElementIdentity rootGroupSelection);
            AssertReadySnapshot(
                await harness.QueueAndWaitAsync(rootGroup, sourceRevision++),
                rootGroup,
                hostSourceRevision: 2);

            string withCircle = CreateElement(
                rootGroup,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Circle,
                out _);
            AssertReadySnapshot(
                await harness.QueueAndWaitAsync(withCircle, sourceRevision++),
                withCircle,
                hostSourceRevision: 3);

            string nestedGroup = CreateElement(
                withCircle,
                rootGroupSelection,
                SvgCreateDestination.SelectedContext,
                SvgCreateElementKind.Group,
                out _);
            AssertReadySnapshot(
                await harness.QueueAndWaitAsync(nestedGroup, sourceRevision++),
                nestedGroup,
                hostSourceRevision: 4);

            string rapidOne = CreateElement(
                nestedGroup,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Rectangle,
                out _);
            string rapidTwo = CreateElement(
                rapidOne,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Ellipse,
                out _);
            string rapidLatest = CreateElement(
                rapidTwo,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Text,
                out _);
            harness.Queue(rapidOne, sourceRevision++);
            harness.Queue(rapidTwo, sourceRevision++);
            long rapidLatestSourceRevision = sourceRevision++;
            BrowserSnapshot rapidReady = await harness.QueueAndWaitAsync(
                rapidLatest,
                rapidLatestSourceRevision);
            AssertReadySnapshot(
                rapidReady,
                rapidLatest,
                rapidLatestSourceRevision);
            Assert.IsFalse(harness.ReadySourceRevisions.Contains(
                rapidLatestSourceRevision - 1));

            // Undo and redo rebuild both the source index and isolated page.
            AssertReadySnapshot(
                await harness.QueueAndWaitAsync(rapidTwo, sourceRevision++),
                rapidTwo,
                hostSourceRevision: 8);
            AssertReadySnapshot(
                await harness.QueueAndWaitAsync(rapidLatest, sourceRevision++),
                rapidLatest,
                hostSourceRevision: 9);

            string completionRaceSource = CreateElement(
                rapidLatest,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Line,
                out _);
            string completionRaceLatest = CreateElement(
                completionRaceSource,
                selection: null,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Group,
                out _);
            long completingSourceRevision = sourceRevision++;
            long arrivingSourceRevision = sourceRevision++;
            harness.BeforeNextNavigationCompletion = () =>
                harness.Queue(completionRaceLatest, arrivingSourceRevision);
            harness.Queue(completionRaceSource, completingSourceRevision);
            BrowserSnapshot raceReady = await harness.WaitForReadyAsync(
                arrivingSourceRevision);
            WriteSnapshot("latest-authoring-ready", raceReady);
            AssertReadySnapshot(
                raceReady,
                completionRaceLatest,
                arrivingSourceRevision);
            Assert.IsFalse(harness.ReadySourceRevisions.Contains(
                completingSourceRevision));

            Task blockedBlank = harness.WaitForBlockedNavigationAsync();
            core.Navigate("about:blank");
            await blockedBlank.WaitAsync(TimeSpan.FromSeconds(5));
            BrowserSnapshot afterBlockedBlank =
                await harness.CaptureCurrentAsync(completionRaceLatest);
            AssertReadySnapshot(
                afterBlockedBlank,
                completionRaceLatest,
                arrivingSourceRevision);
            Assert.AreEqual(1.0, webView.ZoomFactor, 0.0001);

            TaskCompletionSource<string> deleteReceived = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnAuthoringCommand(
                object? sender,
                CoreWebView2WebMessageReceivedEventArgs args)
            {
                using JsonDocument json = JsonDocument.Parse(
                    args.WebMessageAsJson);
                if (json.RootElement.TryGetProperty(
                        "type",
                        out JsonElement type)
                    && type.GetString() == "authoringCommand")
                {
                    deleteReceived.TrySetResult(args.WebMessageAsJson);
                }
            }

            core.WebMessageReceived += OnAuthoringCommand;
            try
            {
                await core.ExecuteScriptAsync(
                    """
                    window.dispatchEvent(new KeyboardEvent('keydown', {
                      code: 'Delete',
                      key: 'Delete',
                      bubbles: true,
                      cancelable: true
                    }))
                    """);
                string deleteJson = await deleteReceived.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
                Assert.IsTrue(
                    new PreviewInteractionMessageParser()
                        .TryParseAuthoringCommand(
                            deleteJson,
                            harness.CurrentToken,
                            expectedSourceRevision: arrivingSourceRevision,
                            out PreviewAuthoringCommand command));
                Assert.AreEqual(PreviewAuthoringCommand.Delete, command);
            }
            finally
            {
                core.WebMessageReceived -= OnAuthoringCommand;
            }

            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            window?.Close();
            webView?.Dispose();
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Background);
            TryDeleteTestProfile(userDataFolder);
        }
    }

    private static string CreateElement(
        string source,
        SvgElementIdentity? selection,
        SvgCreateDestination destination,
        SvgCreateElementKind kind,
        out SvgElementIdentity preferredSelection)
    {
        SvgDocumentIndexResult indexResult =
            new SvgDocumentIndexService().Build(source);
        Assert.IsTrue(indexResult.Validation.IsValid);
        SvgDocumentIndex document = indexResult.Document!;
        SvgElementNode? selected = selection is null
            ? null
            : document.FindBestMatch(selection);
        SvgAuthoringEditResult result = new SvgElementCreationService().CreateEdit(
            source,
            document,
            selected,
            destination,
            kind,
            new SvgCanvasSizeReader().Read(source));
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        Assert.IsNotNull(result.PreferredSelection);
        preferredSelection = result.PreferredSelection;
        return result.Edit.Apply(source);
    }

    private static async Task<BrowserSnapshot> LoadAndCaptureAsync(
        CoreWebView2 core,
        WebView2CompositionControl webView,
        PreviewHtmlBuilder builder,
        string svg,
        long sourceRevision,
        string token)
    {
        SvgDocumentIndex document = new SvgDocumentIndexService()
            .Build(svg).Document!;
        SvgCanvasSize canvas = new SvgCanvasSizeReader().Read(svg);
        SvgVisualDocument visual = new SvgVisualGeometryIndexService().Build(
            document,
            canvas,
            svg);
        TaskCompletionSource<string> imageState = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnMessage(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            using JsonDocument json = JsonDocument.Parse(args.WebMessageAsJson);
            if (json.RootElement.TryGetProperty("type", out JsonElement type)
                && type.GetString() == "imageState")
            {
                imageState.TrySetResult(args.WebMessageAsJson);
            }
        }

        core.WebMessageReceived += OnMessage;
        try
        {
            await NavigateAsync(
                core,
                builder.Build(
                    svg,
                    canvas.Width,
                    canvas.Height,
                    token,
                    sourceRevision: sourceRevision,
                    visualViewport: visual.Viewport));
            string messageJson = await imageState.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            Assert.IsTrue(new PreviewInteractionMessageParser()
                .TryParseImageLoadState(
                    messageJson,
                    token,
                    sourceRevision,
                    out PreviewImageLoadMessage message));
            Assert.AreEqual(PreviewImageLoadState.Loaded, message.State);
        }
        finally
        {
            core.WebMessageReceived -= OnMessage;
        }

        return await CaptureBrowserSnapshotAsync(
            core,
            webView,
            svg,
            token,
            sourceRevision,
            latestRequestedRenderRevision: 0,
            latestCompletedRenderRevision: 0);
    }

    private static async Task NavigateToBlankAsync(CoreWebView2 core)
    {
        TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs> completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args) =>
            completed.TrySetResult(args);
        core.NavigationCompleted += OnCompleted;
        try
        {
            core.Navigate("about:blank");
            CoreWebView2NavigationCompletedEventArgs result =
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(result.IsSuccess, result.WebErrorStatus.ToString());
        }
        finally
        {
            core.NavigationCompleted -= OnCompleted;
        }
    }

    private static async Task NavigateAsync(CoreWebView2 core, string html)
    {
        TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs> completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
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
            Assert.IsTrue(result.IsSuccess, result.WebErrorStatus.ToString());
        }
        finally
        {
            core.NavigationCompleted -= OnCompleted;
        }
    }

    private static async Task<BrowserSnapshot> CaptureBrowserSnapshotAsync(
        CoreWebView2 core,
        WebView2CompositionControl webView,
        string expectedSvg,
        string expectedToken,
        long hostSourceRevision,
        long latestRequestedRenderRevision,
        long latestCompletedRenderRevision)
    {
        string expectedImageSource = "data:image/svg+xml;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(expectedSvg));
        string expectedImageJson = JsonSerializer.Serialize(expectedImageSource);
        string script = $$"""
            (() => {
              const image = document.querySelector('img');
              const viewport = document.querySelector('.preview-viewport');
              const style = image ? getComputedStyle(image) : null;
              const rect = image ? image.getBoundingClientRect() : null;
              let canvasDrawSucceeded = false;
              if (image && image.complete && image.naturalWidth > 0) {
                try {
                  const canvas = document.createElement('canvas');
                  canvas.width = 1;
                  canvas.height = 1;
                  const context = canvas.getContext('2d', { alpha: true });
                  context.drawImage(image, 0, 0, 1, 1);
                  context.getImageData(0, 0, 1, 1);
                  canvasDrawSucceeded = true;
                } catch {
                  canvasDrawSucceeded = false;
                }
              }
              return {
                location: location.href,
                hostScriptReady:
                  document.body?.dataset.hostScriptReady === 'true',
                checkerboardExists: !!viewport &&
                  getComputedStyle(document.body).backgroundImage !== 'none',
                pageToken: document.body?.dataset.bridgeToken || '',
                pageSourceRevision: Number.parseInt(
                  document.body?.dataset.sourceRevision || '-1', 10),
                imageExists: !!image,
                imageSourcePresent: !!image?.getAttribute('src'),
                imageSourceCurrent:
                  image?.getAttribute('src') === {{expectedImageJson}},
                imageComplete: image?.complete === true,
                naturalWidth: image?.naturalWidth || 0,
                naturalHeight: image?.naturalHeight || 0,
                renderedWidth: rect?.width || 0,
                renderedHeight: rect?.height || 0,
                computedWidth: style?.width || '',
                computedHeight: style?.height || '',
                display: style?.display || '',
                visibility: style?.visibility || '',
                opacity: style?.opacity || '',
                loadEvent: image?.dataset.loadEvent || '',
                presented: image?.dataset.presented === 'true',
                canvasDrawSucceeded
              };
            })()
            """;
        string result = await core.ExecuteScriptAsync(script);
        BrowserDomSnapshot dom = JsonSerializer.Deserialize<BrowserDomSnapshot>(
            result,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "WebView2 returned no browser snapshot.");
        return new BrowserSnapshot(
            hostSourceRevision,
            latestRequestedRenderRevision,
            latestCompletedRenderRevision,
            expectedToken,
            core.Source,
            webView.Visibility,
            webView.ActualWidth,
            webView.ActualHeight,
            webView.ZoomFactor,
            dom);
    }

    private static void AssertReadySnapshot(
        BrowserSnapshot snapshot,
        string expectedSvg,
        long hostSourceRevision,
        long? latestRequestedRenderRevision = null,
        long? latestCompletedRenderRevision = null,
        string? navigationToken = null)
    {
        Assert.IsTrue(expectedSvg.Length > 0);
        Assert.AreEqual(hostSourceRevision, snapshot.HostSourceRevision);
        Assert.AreEqual(
            latestRequestedRenderRevision
                ?? snapshot.LatestRequestedRenderRevision,
            snapshot.LatestRequestedRenderRevision);
        Assert.AreEqual(
            latestCompletedRenderRevision
                ?? snapshot.LatestCompletedRenderRevision,
            snapshot.LatestCompletedRenderRevision);
        Assert.AreEqual(
            snapshot.LatestRequestedRenderRevision,
            snapshot.LatestCompletedRenderRevision,
            "Ready must describe the latest requested render revision.");
        Assert.AreEqual(
            navigationToken ?? snapshot.NavigationToken,
            snapshot.NavigationToken);
        Assert.AreEqual(snapshot.NavigationToken, snapshot.Dom.PageToken);
        Assert.AreEqual(hostSourceRevision, snapshot.Dom.PageSourceRevision);
        Assert.IsTrue(snapshot.Dom.HostScriptReady);
        Assert.IsTrue(snapshot.Dom.CheckerboardExists);
        Assert.IsTrue(snapshot.Dom.ImageExists);
        Assert.IsTrue(snapshot.Dom.ImageSourcePresent);
        Assert.IsTrue(snapshot.Dom.ImageSourceCurrent);
        Assert.IsTrue(snapshot.Dom.ImageComplete);
        Assert.IsTrue(snapshot.Dom.NaturalWidth > 0);
        Assert.IsTrue(snapshot.Dom.NaturalHeight > 0);
        Assert.IsTrue(snapshot.Dom.RenderedWidth > 0);
        Assert.IsTrue(snapshot.Dom.RenderedHeight > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.Dom.ComputedWidth));
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.Dom.ComputedHeight));
        Assert.AreEqual("block", snapshot.Dom.Display);
        Assert.AreEqual("visible", snapshot.Dom.Visibility);
        Assert.AreEqual("1", snapshot.Dom.Opacity);
        Assert.IsTrue(snapshot.Dom.LoadEvent is "load" or "complete-before-listener");
        Assert.IsTrue(snapshot.Dom.Presented);
        Assert.IsTrue(snapshot.Dom.CanvasDrawSucceeded);
        Assert.AreEqual(Visibility.Visible, snapshot.WebViewVisibility);
        Assert.IsTrue(snapshot.WebViewWidth > 0);
        Assert.IsTrue(snapshot.WebViewHeight > 0);
        Assert.AreEqual(1.0, snapshot.WebViewZoomFactor, 0.0001);
    }

    private static void WriteSnapshot(string label, BrowserSnapshot snapshot)
    {
        Console.WriteLine(
            $"{label}: hostSource={snapshot.HostSourceRevision}, "
            + $"requestedRender={snapshot.LatestRequestedRenderRevision}, "
            + $"completedRender={snapshot.LatestCompletedRenderRevision}, "
            + $"token={snapshot.NavigationToken}, "
            + $"coreSource={snapshot.CoreSource}, location={snapshot.Dom.Location}, "
            + $"trusted={snapshot.Dom.HostScriptReady}, "
            + $"checkerboard={snapshot.Dom.CheckerboardExists}, "
            + $"image={snapshot.Dom.ImageExists}, "
            + $"srcCurrent={snapshot.Dom.ImageSourceCurrent}, "
            + $"complete={snapshot.Dom.ImageComplete}, "
            + $"natural={snapshot.Dom.NaturalWidth}x{snapshot.Dom.NaturalHeight}, "
            + $"rendered={snapshot.Dom.RenderedWidth}x{snapshot.Dom.RenderedHeight}, "
            + $"css={snapshot.Dom.ComputedWidth}x{snapshot.Dom.ComputedHeight}, "
            + $"display={snapshot.Dom.Display}, visibility={snapshot.Dom.Visibility}, "
            + $"opacity={snapshot.Dom.Opacity}, loadEvent={snapshot.Dom.LoadEvent}, "
            + $"presented={snapshot.Dom.Presented}, "
            + $"webView={snapshot.WebViewVisibility}/"
            + $"{snapshot.WebViewWidth}x{snapshot.WebViewHeight}/"
            + $"zoom={snapshot.WebViewZoomFactor}");
    }

    private sealed class BrowserLifecycleHarness : IDisposable
    {
        private readonly CoreWebView2 _core;
        private readonly WebView2CompositionControl _webView;
        private readonly PreviewHtmlBuilder _builder;
        private readonly PreviewNavigationPolicy _navigationPolicy = new();
        private readonly PreviewNavigationCoordinator _coordinator = new();
        private readonly PreviewRenderReadiness _readiness = new();
        private readonly PreviewInteractionMessageParser _parser = new();
        private readonly SvgDocumentIndexService _indexService = new();
        private readonly SvgCanvasSizeReader _canvasReader = new();
        private readonly SvgVisualGeometryIndexService _geometryService = new();
        private readonly Dictionary<long, TaskCompletionSource<BrowserSnapshot>>
            _ready = [];
        private readonly HashSet<long> _readySourceRevisions = [];
        private PreviewRenderRequest? _active;
        private bool _isHostNavigationRequested;
        private ulong? _activeNavigationId;
        private long _latestRequestedRenderRevision;
        private long _latestCompletedRenderRevision;
        private string _currentToken = string.Empty;
        private string _currentExpectedSvg = string.Empty;
        private long _lastReadySourceRevision;
        private TaskCompletionSource<bool>? _blockedNavigation;

        public BrowserLifecycleHarness(
            CoreWebView2 core,
            WebView2CompositionControl webView,
            PreviewHtmlBuilder builder)
        {
            _core = core;
            _webView = webView;
            _builder = builder;
            _core.NavigationStarting += OnNavigationStarting;
            _core.NavigationCompleted += OnNavigationCompleted;
            _core.WebMessageReceived += OnWebMessageReceived;
        }

        public IReadOnlySet<long> ReadySourceRevisions =>
            _readySourceRevisions;

        public string CurrentToken => _currentToken;

        public Action? BeforeNextNavigationCompletion { get; set; }

        public void Queue(string svg, long sourceRevision)
        {
            SvgDocumentIndexResult indexResult = _indexService.Build(svg);
            Assert.IsTrue(indexResult.Validation.IsValid);
            SvgDocumentIndex document = indexResult.Document!;
            SvgCanvasSize canvas = _canvasReader.Read(svg);
            SvgVisualDocument visual = _geometryService.Build(
                document,
                canvas,
                svg);
            Assert.IsTrue(_coordinator.TryEnqueue(
                sourceRevision,
                svg,
                canvas,
                visual,
                PreviewZoomState.Fit,
                PreviewViewportPosition.Center,
                force: false,
                out PreviewRenderRequest? request));
            Assert.IsNotNull(request);
            _latestRequestedRenderRevision = request.Revision;
            GetReadyCompletion(sourceRevision);
            StartPendingNavigation();
        }

        public async Task<BrowserSnapshot> QueueAndWaitAsync(
            string svg,
            long sourceRevision)
        {
            Queue(svg, sourceRevision);
            return await WaitForReadyAsync(sourceRevision);
        }

        public Task<BrowserSnapshot> WaitForReadyAsync(long sourceRevision)
        {
            TaskCompletionSource<BrowserSnapshot> ready =
                GetReadyCompletion(sourceRevision);
            return ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        private TaskCompletionSource<BrowserSnapshot> GetReadyCompletion(
            long sourceRevision)
        {
            if (!_ready.TryGetValue(
                    sourceRevision,
                    out TaskCompletionSource<BrowserSnapshot>? ready))
            {
                ready = new TaskCompletionSource<BrowserSnapshot>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _ready.Add(sourceRevision, ready);
            }
            return ready;
        }

        public Task WaitForBlockedNavigationAsync()
        {
            _blockedNavigation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _blockedNavigation.Task;
        }

        public Task<BrowserSnapshot> CaptureCurrentAsync(string expectedSvg) =>
            CaptureBrowserSnapshotAsync(
                _core,
                _webView,
                expectedSvg,
                _currentToken,
                _active?.SourceRevision ?? _lastReadySourceRevision,
                _latestRequestedRenderRevision,
                _latestCompletedRenderRevision);

        private void StartPendingNavigation()
        {
            if (_coordinator.TryBeginNext() is not PreviewRenderRequest request)
            {
                return;
            }

            _active = request;
            _currentToken = request.Revision.ToString("X32");
            _currentExpectedSvg = request.Svg;
            _activeNavigationId = null;
            _readiness.Begin(request.Revision, request.SourceRevision);
            _isHostNavigationRequested = true;
            _core.NavigateToString(_builder.Build(
                request.Svg,
                request.CanvasSize.Width,
                request.CanvasSize.Height,
                _currentToken,
                request.Viewport,
                request.SourceRevision,
                request.VisualDocument.Viewport));
        }

        private void OnNavigationStarting(
            object? sender,
            CoreWebView2NavigationStartingEventArgs e)
        {
            if (!_navigationPolicy.IsAllowed(
                    e.Uri,
                    _isHostNavigationRequested))
            {
                e.Cancel = true;
                _blockedNavigation?.TrySetResult(true);
                _blockedNavigation = null;
                return;
            }

            _isHostNavigationRequested = false;
            _activeNavigationId = e.NavigationId;
        }

        private void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_active is not PreviewRenderRequest request
                || _activeNavigationId != e.NavigationId)
            {
                return;
            }

            _activeNavigationId = null;
            Action? beforeCompletion = BeforeNextNavigationCompletion;
            BeforeNextNavigationCompletion = null;
            beforeCompletion?.Invoke();
            if (_coordinator.HasPending)
            {
                Complete(request, isSuccess: false);
                return;
            }

            Resolve(
                request,
                _readiness.RecordNavigation(
                    request.Revision,
                    e.IsSuccess));
        }

        private void OnWebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_active is not PreviewRenderRequest request
                || !_navigationPolicy.IsTrustedWebMessageSource(e.Source)
                || !_parser.TryParseImageLoadState(
                    e.WebMessageAsJson,
                    _currentToken,
                    request.SourceRevision,
                    out PreviewImageLoadMessage message))
            {
                return;
            }

            Resolve(
                request,
                _readiness.RecordImage(request.Revision, message));
        }

        private void Resolve(
            PreviewRenderRequest request,
            PreviewRenderReadinessResult result)
        {
            if (result is PreviewRenderReadinessResult.Ignored
                or PreviewRenderReadinessResult.Waiting)
            {
                return;
            }

            Complete(
                request,
                result == PreviewRenderReadinessResult.Ready);
        }

        private void Complete(
            PreviewRenderRequest request,
            bool isSuccess)
        {
            if (!_coordinator.TryComplete(
                    request.Revision,
                    isSuccess,
                    out bool wasLatest))
            {
                return;
            }

            _latestCompletedRenderRevision = request.Revision;
            _readiness.Reset();
            _active = null;
            if (_coordinator.HasPending)
            {
                StartPendingNavigation();
                return;
            }

            if (!isSuccess || !wasLatest)
            {
                _ready[request.SourceRevision].TrySetException(
                    new AssertFailedException(
                        $"Render {request.Revision} did not become Ready."));
                return;
            }

            _ = CaptureReadyAsync(
                request,
                _currentToken,
                _currentExpectedSvg);
        }

        private async Task CaptureReadyAsync(
            PreviewRenderRequest request,
            string token,
            string expectedSvg)
        {
            try
            {
                BrowserSnapshot snapshot = await CaptureBrowserSnapshotAsync(
                    _core,
                    _webView,
                    expectedSvg,
                    token,
                    request.SourceRevision,
                    _latestRequestedRenderRevision,
                    _latestCompletedRenderRevision);
                _lastReadySourceRevision = request.SourceRevision;
                _readySourceRevisions.Add(request.SourceRevision);
                _ready[request.SourceRevision].TrySetResult(snapshot);
            }
            catch (Exception exception)
            {
                _ready[request.SourceRevision].TrySetException(exception);
            }
        }

        public void Dispose()
        {
            _core.NavigationStarting -= OnNavigationStarting;
            _core.NavigationCompleted -= OnNavigationCompleted;
            _core.WebMessageReceived -= OnWebMessageReceived;
        }
    }

    private sealed record BrowserSnapshot(
        long HostSourceRevision,
        long LatestRequestedRenderRevision,
        long LatestCompletedRenderRevision,
        string NavigationToken,
        string CoreSource,
        Visibility WebViewVisibility,
        double WebViewWidth,
        double WebViewHeight,
        double WebViewZoomFactor,
        BrowserDomSnapshot Dom);

    private sealed record BrowserDomSnapshot(
        string Location,
        bool HostScriptReady,
        bool CheckerboardExists,
        string PageToken,
        long PageSourceRevision,
        bool ImageExists,
        bool ImageSourcePresent,
        bool ImageSourceCurrent,
        bool ImageComplete,
        double NaturalWidth,
        double NaturalHeight,
        double RenderedWidth,
        double RenderedHeight,
        string ComputedWidth,
        string ComputedHeight,
        string Display,
        string Visibility,
        string Opacity,
        string LoadEvent,
        bool Presented,
        bool CanvasDrawSucceeded);

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
