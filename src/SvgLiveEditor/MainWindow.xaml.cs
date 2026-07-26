using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Indentation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions = [".svg", ".txt"];

    private readonly MainViewModel _viewModel = new();
    private readonly SvgDocumentIndexService _documentIndexService = new();
    private readonly PreviewHtmlBuilder _previewHtmlBuilder = new();
    private readonly PreviewNavigationPolicy _previewNavigationPolicy = new();
    private readonly Utf8FileService _fileService = new();
    private readonly WelcomeSvgProvider _welcomeSvgProvider = new();
    private readonly UnsavedChangesPolicy _unsavedChangesPolicy = new();
    private readonly AsyncDebouncer _previewDebouncer = new(TimeSpan.FromMilliseconds(300));
    private readonly PreviewZoomCalculator _previewZoomCalculator = new();
    private readonly PreviewZoomBridge _previewZoomBridge = new();
    private readonly PreviewInteractionMessageParser _previewInteractionMessageParser = new();
    private readonly PreviewViewportCalculator _previewViewportCalculator = new();
    private readonly PreviewNavigationCoordinator _previewNavigationCoordinator = new();
    private readonly PreviewPageMessageBuilder _previewPageMessageBuilder = new();
    private readonly PreviewUpdatePolicy _previewUpdatePolicy = new();
    private readonly SvgCanvasSizeReader _svgCanvasSizeReader = new();
    private readonly PreviewPngCopyPolicy _previewPngCopyPolicy = new();
    private readonly PreviewPngMessageParser _previewPngMessageParser = new();
    private readonly ClipboardCopyService _clipboardCopyService =
        new(new WindowsClipboardWriter());
    private readonly UserPreferencesService _userPreferencesService = new();
    private readonly LastDocumentService _lastDocumentService = new();
    private readonly WebView2UserDataFolderProvider _webView2UserDataFolderProvider = new();
    private readonly SourceRevisionTracker _sourceRevisionTracker = new();
    private readonly ApplicationInfoService _applicationInfoService = new();
    private readonly DispatcherTimer _fitResizeTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };

    private bool _isUpdatingEditor;
    private bool _isWebViewReady;
    private bool _hasVisiblePreview;
    private bool _isPreviewNavigationRequested;
    private bool _isPanModeEnabled;
    private PreviewPngSourceState _previewPngSourceState =
        PreviewPngSourceState.PendingValidation;
    private string _previewPresentationState = "Loading";
    private UserPreferences _userPreferences = UserPreferences.Default;
    private PreviewZoomState _previewZoomState = PreviewZoomState.Fit;
    private PreviewViewportPosition _previewViewport = PreviewViewportPosition.Center;
    private string? _lastValidSvg;
    private SvgCanvasSize? _lastValidCanvasSize;
    private ulong? _activePreviewNavigationId;
    private long? _activePreviewRevision;
    private string? _activePreviewBridgeToken;
    private CoreWebView2? _configuredCoreWebView;
    private Task<bool>? _webViewInitializationTask;
    private PendingPreviewPngCopy? _pendingPreviewPngCopy;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        SourceEditor.Options.IndentationSize = 2;
        SourceEditor.Options.ConvertTabsToSpaces = true;
        SourceEditor.Options.AllowScrollBelowDocument = true;
        SourceEditor.Options.EnableTextDragDrop = false;
        SourceEditor.Options.HighlightCurrentLine = true;
        SourceEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();
        SourceEditor.Document.TextChanged += OnEditorDocumentTextChanged;
        SourceEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        PreviewWebView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
        _fitResizeTimer.Tick += OnFitResizeTimerTick;
        InitializeDocumentInspector();

        _userPreferences = _userPreferencesService.Load();
        _previewZoomState = _userPreferences.PreviewZoom;
        ApplyWordWrap(_userPreferences.WordWrap, persist: false);
        ReopenLastDocumentMenuItem.IsChecked =
            _userPreferences.ReopenLastDocumentOnStartup;
        UpdatePreviewStateText(_previewPresentationState);

        LoadStartupDocument();
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (await EnsureWebViewReadyAsync())
        {
            await RefreshPreviewNowAsync();
        }

        SourceEditor.Focus();
    }

    private async Task<bool> EnsureWebViewReadyAsync()
    {
        if (_isWebViewReady && PreviewWebView.CoreWebView2 is not null)
        {
            return true;
        }

        _webViewInitializationTask ??= InitializeWebViewAsync();
        Task<bool> initializationTask = _webViewInitializationTask;
        try
        {
            return await initializationTask;
        }
        finally
        {
            if (ReferenceEquals(_webViewInitializationTask, initializationTask))
            {
                _webViewInitializationTask = null;
            }
        }
    }

    private async Task<bool> InitializeWebViewAsync()
    {
        ShowPreviewLoading("Starting the secure WebView2 preview...");

        try
        {
            string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(runtimeVersion))
            {
                throw new WebView2RuntimeNotFoundException("No compatible WebView2 Runtime was found.");
            }

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: _webView2UserDataFolderProvider.GetPath());
            await PreviewWebView.EnsureCoreWebView2Async(environment);
            CoreWebView2 core = PreviewWebView.CoreWebView2
                ?? throw new InvalidOperationException("WebView2 initialized without a CoreWebView2 instance.");

            ConfigureWebViewSecurity(core);
            PreviewWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 248, 250, 252);
            PreviewWebView.ZoomFactor = 1.0;

            // Settle any startup navigation before issuing the host's trusted data:text/html document.
            core.Stop();
            _isWebViewReady = true;
            return true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowPreviewError(
                "WebView2 Runtime required",
                "SvgLiveEditor uses the Microsoft Edge WebView2 Evergreen Runtime for its preview. Install the official runtime, then click Refresh Preview.",
                showRuntimeLink: true);
        }
        catch (Exception exception)
        {
            ShowPreviewError(
                "Preview could not start",
                $"WebView2 initialization failed: {exception.Message}");
        }
        return false;
    }

    private void OnCoreWebView2InitializationCompleted(
        object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ShowPreviewError(
                "Preview could not start",
                $"WebView2 initialization failed: {e.InitializationException?.Message ?? "Unknown initialization error."}");
            return;
        }

        if (PreviewWebView.CoreWebView2 is CoreWebView2 core)
        {
            ConfigureWebViewSecurity(core);
        }
    }

    private void ConfigureWebViewSecurity(CoreWebView2 core)
    {
        if (ReferenceEquals(_configuredCoreWebView, core))
        {
            return;
        }

        DetachCoreWebViewEvents();
        _configuredCoreWebView = core;

        CoreWebView2Settings settings = core.Settings;
        // Only the CSP-hashed host interaction script runs. The untrusted SVG remains an
        // isolated data image and cannot access this scripting context.
        settings.IsScriptEnabled = true;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = true;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        // Physical Ctrl+Wheel is suppressed before DOM dispatch when this is false.
        // The trusted page captures and cancels it, then requests artwork-only zoom.
        settings.IsZoomControlEnabled = true;
        settings.IsPinchZoomEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;

        core.NavigationStarting += OnPreviewNavigationStarting;
        core.NavigationCompleted += OnPreviewNavigationCompleted;
        core.ProcessFailed += OnPreviewProcessFailed;
        core.WebMessageReceived += OnPreviewWebMessageReceived;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.DownloadStarting += (_, args) => args.Cancel = true;
        core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) => BlockUnexpectedWebResource(core, args);
    }

    private void OnPreviewNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_previewNavigationPolicy.IsAllowed(e.Uri, _isPreviewNavigationRequested))
        {
            e.Cancel = true;
            return;
        }

        if (_isPreviewNavigationRequested)
        {
            CancelPendingPreviewPngCopy(
                "Preview changed before the PNG copy completed. Try again.");
            _isPreviewNavigationRequested = false;
            _activePreviewNavigationId = e.NavigationId;
        }
    }

    private void OnPreviewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_activePreviewNavigationId != e.NavigationId
            || _activePreviewRevision is not long revision)
        {
            return;
        }

        _activePreviewNavigationId = null;
        _activePreviewRevision = null;
        if (!_previewNavigationCoordinator.TryComplete(revision, out bool wasLatest))
        {
            return;
        }

        if (_previewNavigationCoordinator.HasPending)
        {
            StartPendingPreviewNavigation();
            return;
        }

        if (e.IsSuccess && wasLatest)
        {
            ShowPreviewReady();
            TryUpdatePreviewZoomInPlace();
            TryUpdatePreviewPanModeInPlace();
            return;
        }

        CancelPendingPreviewPngCopy();
        _activePreviewBridgeToken = null;
        ShowPreviewError(
            "Preview could not be rendered",
            $"WebView2 navigation failed with {e.WebErrorStatus}. Click Refresh Preview to retry.");
    }

    private void OnPreviewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _isWebViewReady = false;
        _isPreviewNavigationRequested = false;
        _activePreviewNavigationId = null;
        _activePreviewRevision = null;
        _activePreviewBridgeToken = null;
        CancelPendingPreviewPngCopy();
        _previewNavigationCoordinator.Reset();
        ShowPreviewError(
            "Preview process failed",
            $"WebView2 reported {e.ProcessFailedKind} ({e.Reason}, exit code {e.ExitCode}). Click Refresh Preview to retry.");
    }

    private async void OnPreviewWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_isWebViewReady
            || _activePreviewBridgeToken is not string bridgeToken
            || !_previewNavigationPolicy.IsTrustedWebMessageSource(e.Source))
        {
            return;
        }

        string messageJson = e.WebMessageAsJson;
        if (_pendingPreviewPngCopy is PendingPreviewPngCopy pending)
        {
            if (_previewPngMessageParser.IsMatchingError(
                    messageJson,
                    pending))
            {
                _pendingPreviewPngCopy = null;
                _viewModel.SetCanCopyPreviewAsPng(_hasVisiblePreview);
                _viewModel.SetOperationStatus(
                    "The preview could not be converted to PNG.");
                return;
            }

            PreviewPngPayload? pngPayload = null;
            bool isPng = await Task.Run(() =>
                _previewPngMessageParser.TryParse(
                    messageJson,
                    pending,
                    out pngPayload));
            if (isPng && pngPayload is not null)
            {
                if (!ReferenceEquals(_pendingPreviewPngCopy, pending))
                {
                    return;
                }

                _pendingPreviewPngCopy = null;
                ClipboardWriteResult result =
                    await _clipboardCopyService.CopyPngAsync(pngPayload);
                _viewModel.SetCanCopyPreviewAsPng(_hasVisiblePreview);
                if (result.Succeeded)
                {
                    string dimensions =
                        $"{pngPayload.Size.Width} \u00D7 {pngPayload.Size.Height}";
                    _viewModel.SetOperationStatus(
                        pending.Plan.SourceState switch
                        {
                            PreviewPngSourceState.CurrentInvalid =>
                                $"Copied the last valid preview; current source is invalid \u00B7 {dimensions}",
                            PreviewPngSourceState.PendingValidation =>
                                $"Copied the last validated preview; current source is still validating \u00B7 {dimensions}",
                            _ =>
                                $"Preview copied as PNG \u00B7 {dimensions}"
                        });
                }
                else
                {
                    _viewModel.SetOperationStatus(
                        $"Could not copy the preview: {result.ErrorMessage}");
                }
                return;
            }

            if (messageJson.Length > 16_384)
            {
                return;
            }
        }
        else if (messageJson.Length > 16_384)
        {
            return;
        }

        if (_previewInteractionMessageParser.TryParseViewportPosition(
                messageJson,
                bridgeToken,
                out PreviewViewportPosition viewport))
        {
            if (_previewZoomState.Mode == PreviewZoomMode.Manual)
            {
                _previewViewport = viewport;
            }
            return;
        }

        if (_previewInteractionMessageParser.TryParsePanCommand(
                messageJson,
                bridgeToken,
                out PreviewPanCommand panCommand))
        {
            SetPanMode(
                panCommand == PreviewPanCommand.Toggle
                    ? !_isPanModeEnabled
                    : false);
            return;
        }

        if (_lastValidCanvasSize is not SvgCanvasSize canvasSize
            || !_previewInteractionMessageParser.TryParseZoomRequest(
                messageJson,
                bridgeToken,
                out PreviewZoomRequest request))
        {
            return;
        }

        PreviewZoomTransition transition = _previewZoomBridge.Apply(
            _previewZoomState,
            canvasSize,
            GetFitScale(canvasSize),
            request);
        if (transition.State == _previewZoomState)
        {
            return;
        }

        double contentWidth = Math.Max(
            request.ViewportWidth,
            transition.RenderedWidth + (PreviewZoomCalculator.CanvasPadding * 2));
        double contentHeight = Math.Max(
            request.ViewportHeight,
            transition.RenderedHeight + (PreviewZoomCalculator.CanvasPadding * 2));
        _previewViewport = _previewViewportCalculator.Capture(
            transition.Scroll,
            contentWidth,
            contentHeight,
            request.ViewportWidth,
            request.ViewportHeight);
        ApplyPreviewZoomState(transition.State);
    }

    private static void BlockUnexpectedWebResource(CoreWebView2 core, CoreWebView2WebResourceRequestedEventArgs args)
    {
        string uri = args.Request.Uri;
        bool isInitialBlank = args.ResourceContext == CoreWebView2WebResourceContext.Document
            && uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase);
        bool isTrustedHostDocument = args.ResourceContext == CoreWebView2WebResourceContext.Document
            && uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase);
        bool isSvgDataImage = args.ResourceContext == CoreWebView2WebResourceContext.Image
            && uri.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase);

        if (isInitialBlank || isTrustedHostDocument || isSvgDataImage)
        {
            return;
        }

        args.Response = core.Environment.CreateWebResourceResponse(
            Stream.Null,
            403,
            "Blocked by SvgLiveEditor",
            "Content-Type: text/plain; charset=utf-8");
    }

    private void ShowPreviewLoading(string message)
    {
        _hasVisiblePreview = false;
        _viewModel.SetCanCopyPreviewAsPng(false);
        _previewPresentationState = "Loading";
        UpdatePreviewStateText("Loading");
        PreviewStateText.Foreground = Brushes.SlateGray;
        PreviewMessageTitle.Text = "Loading preview";
        PreviewMessageText.Text = message;
        PreviewLoadingIndicator.Visibility = Visibility.Visible;
        PreviewRuntimeLink.Visibility = Visibility.Collapsed;
        PreviewMessagePanel.Visibility = Visibility.Visible;
        PreviewWebView.Visibility = Visibility.Hidden;
    }

    private void ShowPreviewRefreshing()
    {
        _viewModel.SetCanCopyPreviewAsPng(false);
        _previewPresentationState = "Loading";
        UpdatePreviewStateText("Loading");
        PreviewStateText.Foreground = Brushes.SlateGray;
        PreviewMessagePanel.Visibility = Visibility.Collapsed;
        PreviewWebView.Visibility = Visibility.Visible;
    }

    private void ShowPreviewReady()
    {
        _hasVisiblePreview = true;
        _viewModel.SetCanCopyPreviewAsPng(
            _lastValidCanvasSize is not null);
        _previewPresentationState = "Ready";
        UpdatePreviewStateText("Ready");
        PreviewStateText.Foreground = Brushes.DarkGreen;
        PreviewMessagePanel.Visibility = Visibility.Collapsed;
        PreviewWebView.Visibility = Visibility.Visible;
    }

    private void ShowPreviewError(string title, string message, bool showRuntimeLink = false)
    {
        _isWebViewReady = false;
        _hasVisiblePreview = false;
        CancelPendingPreviewPngCopy();
        _viewModel.SetCanCopyPreviewAsPng(false);
        _previewPresentationState = "Error";
        UpdatePreviewStateText("Error");
        PreviewStateText.Foreground = Brushes.Firebrick;
        PreviewMessageTitle.Text = title;
        PreviewMessageText.Text = message;
        PreviewLoadingIndicator.Visibility = Visibility.Collapsed;
        PreviewRuntimeLink.Visibility = showRuntimeLink ? Visibility.Visible : Visibility.Collapsed;
        PreviewMessagePanel.Visibility = Visibility.Visible;
        PreviewWebView.Visibility = Visibility.Hidden;
    }

    private void UpdatePreviewStateText(string state)
    {
        PreviewStateText.Text = $"{_previewZoomState.DisplayText} \u00B7 {state} \u00B7 Secure image mode";
    }

    private async void OnCopyEntireSourceClick(
        object sender,
        RoutedEventArgs e)
    {
        ClipboardWriteResult result =
            await _clipboardCopyService.CopyTextAsync(SourceEditor.Text);
        _viewModel.SetOperationStatus(
            result.Succeeded
                ? "Entire SVG source copied"
                : $"Could not copy the SVG source: {result.ErrorMessage}");
    }

    private void OnCopyPreviewAsPngClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_pendingPreviewPngCopy is not null)
        {
            _viewModel.SetOperationStatus(
                "A preview copy is already in progress.");
            return;
        }

        if (!_previewPngCopyPolicy.TryCreatePlan(
                _hasVisiblePreview,
                _previewPngSourceState,
                _lastValidCanvasSize,
                out PreviewPngCopyPlan? plan)
            || plan is null
            || !_isWebViewReady
            || _activePreviewBridgeToken is not string bridgeToken
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core)
        {
            _viewModel.SetOperationStatus(
                "No valid preview is available to copy.");
            return;
        }

        string requestId =
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        PendingPreviewPngCopy pending =
            new(bridgeToken, requestId, plan);
        try
        {
            _pendingPreviewPngCopy = pending;
            _viewModel.SetCanCopyPreviewAsPng(false);
            _viewModel.SetOperationStatus("Preparing PNG for the clipboard...");
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildPngRequestMessage(
                    bridgeToken,
                    requestId,
                    plan.Size));
            _ = ExpirePreviewPngCopyAsync(pending);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or COMException)
        {
            _pendingPreviewPngCopy = null;
            _viewModel.SetCanCopyPreviewAsPng(_hasVisiblePreview);
            _viewModel.SetOperationStatus(
                $"Could not request the PNG: {exception.Message}");
        }
    }

    private async Task ExpirePreviewPngCopyAsync(
        PendingPreviewPngCopy pending)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (ReferenceEquals(_pendingPreviewPngCopy, pending))
        {
            CancelPendingPreviewPngCopy(
                "The PNG response was not received or failed validation. Try again.");
        }
    }

    private void CancelPendingPreviewPngCopy(string? status = null)
    {
        if (_pendingPreviewPngCopy is null)
        {
            return;
        }

        _pendingPreviewPngCopy = null;
        _viewModel.SetCanCopyPreviewAsPng(_hasVisiblePreview);
        if (!string.IsNullOrWhiteSpace(status))
        {
            _viewModel.SetOperationStatus(status);
        }
    }

    private void OnEditorDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditor)
        {
            return;
        }

        _sourceRevisionTracker.Advance();
        _previewPngSourceState =
            PreviewPngSourceState.PendingValidation;
        _viewModel.UpdateTextFromEditor(SourceEditor.Text);
        MarkDocumentInspectorSourceChanged();
        QueuePreviewUpdate();
    }

    private void QueuePreviewUpdate()
    {
        string sourceSnapshot = SourceEditor.Text;
        long sourceRevision = _sourceRevisionTracker.Current;
        _ = _previewDebouncer.DebounceAsync(async cancellationToken =>
        {
            SvgDocumentIndexResult result = await Task.Run(
                () => _documentIndexService.Build(sourceSnapshot),
                cancellationToken).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(
                () => ApplyValidationResult(sourceSnapshot, sourceRevision, result),
                System.Windows.Threading.DispatcherPriority.Background,
                cancellationToken);
        });
    }

    private async Task RefreshPreviewNowAsync()
    {
        _previewDebouncer.Cancel();
        string sourceSnapshot = SourceEditor.Text;
        long sourceRevision = _sourceRevisionTracker.Current;
        SvgDocumentIndexResult result = await Task.Run(
            () => _documentIndexService.Build(sourceSnapshot));
        ApplyValidationResult(sourceSnapshot, sourceRevision, result);
    }

    private void ApplyValidationResult(
        string sourceSnapshot,
        long sourceRevision,
        SvgDocumentIndexResult indexResult)
    {
        if (!_sourceRevisionTracker.IsCurrent(sourceRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        SvgValidationResult result = indexResult.Validation;
        _previewPngSourceState = result.IsValid
            ? PreviewPngSourceState.CurrentValid
            : PreviewPngSourceState.CurrentInvalid;
        _viewModel.ApplyValidation(result);
        ApplyDocumentInspectorResult(indexResult);
        if (!result.IsValid)
        {
            if (!_hasVisiblePreview)
            {
                ShowLastValidPreview();
            }
            return;
        }

        _lastValidSvg = sourceSnapshot;
        _lastValidCanvasSize = _svgCanvasSizeReader.Read(sourceSnapshot);
        ShowLastValidPreview();
    }

    private void ShowLastValidPreview()
    {
        if (!_isWebViewReady
            || _lastValidSvg is null
            || _lastValidCanvasSize is not SvgCanvasSize canvasSize
            || PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        _previewNavigationCoordinator.Enqueue(
            _lastValidSvg,
            canvasSize,
            _previewZoomState,
            _previewZoomState.Mode == PreviewZoomMode.Manual
                ? _previewViewport
                : PreviewViewportPosition.Center);
        PreviewUpdateDecision decision = _previewUpdatePolicy.Decide(
            PreviewUpdateKind.Source,
            _hasVisiblePreview);
        if (decision.ShowsFullLoadingState)
        {
            ShowPreviewLoading("Rendering the current valid SVG...");
        }
        else
        {
            ShowPreviewRefreshing();
        }
        StartPendingPreviewNavigation();
    }

    private void StartPendingPreviewNavigation()
    {
        if (!_isWebViewReady
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core
            || _previewNavigationCoordinator.TryBeginNext() is not PreviewRenderRequest request)
        {
            return;
        }

        try
        {
            double fitScale = GetFitScale(request.CanvasSize);
            double scale = _previewZoomCalculator.ResolveScale(request.ZoomState, fitScale);
            double renderedWidth = request.CanvasSize.Width * scale;
            double renderedHeight = request.CanvasSize.Height * scale;
            string bridgeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            string html = _previewHtmlBuilder.Build(
                request.Svg,
                renderedWidth,
                renderedHeight,
                bridgeToken,
                request.Viewport);
            PreviewWebView.UpdateLayout();
            PreviewWebView.ZoomFactor = 1.0;

            _activePreviewNavigationId = null;
            _activePreviewRevision = request.Revision;
            _activePreviewBridgeToken = bridgeToken;
            _isPreviewNavigationRequested = true;
            core.NavigateToString(html);
        }
        catch (Exception exception)
        {
            _isPreviewNavigationRequested = false;
            _activePreviewNavigationId = null;
            _activePreviewRevision = null;
            _activePreviewBridgeToken = null;
            _previewNavigationCoordinator.TryComplete(request.Revision, out _);
            if (_previewNavigationCoordinator.HasPending)
            {
                StartPendingPreviewNavigation();
            }
            else
            {
                ShowPreviewError(
                    "Preview could not be rendered",
                    $"WebView2 could not start the preview navigation: {exception.Message}");
            }
        }
    }

    private double GetFitScale(SvgCanvasSize? canvasSize = null)
    {
        SvgCanvasSize size = canvasSize
            ?? _lastValidCanvasSize
            ?? new SvgCanvasSize(300, 150);
        DpiScale dpi = VisualTreeHelper.GetDpi(PreviewWebView);
        return _previewZoomCalculator.CalculateFitScale(
            size,
            PreviewWebView.ActualWidth,
            PreviewWebView.ActualHeight,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
    }

    private void LoadIntoEditor(string text, string? path)
    {
        _previewDebouncer.Cancel();
        SetPanMode(enabled: false, announce: false);
        _previewPngSourceState =
            PreviewPngSourceState.PendingValidation;
        _previewViewport = PreviewViewportPosition.Center;
        _isUpdatingEditor = true;
        try
        {
            SourceEditor.Text = text;
            _sourceRevisionTracker.Advance();
            SourceEditor.CaretOffset = 0;
            _viewModel.LoadDocument(text, path);
            _viewModel.UpdateCaret(1, 1);
            MarkDocumentInspectorSourceChanged();
            _viewModel.Inspector.ShowUnavailable(
                "Waiting for secure SVG validation.");
        }
        finally
        {
            _isUpdatingEditor = false;
        }

        QueuePreviewUpdate();
    }

    private void LoadStartupDocument()
    {
        string welcomeSource = _welcomeSvgProvider.Load();
        _lastValidSvg = welcomeSource;
        _lastValidCanvasSize = _svgCanvasSizeReader.Read(welcomeSource);

        LastDocumentRestoreResult restore =
            _lastDocumentService.TryRestore(_userPreferences);
        if (restore.IsRestored
            && restore.Source is string source
            && restore.Path is string path)
        {
            LoadIntoEditor(source, path);
            return;
        }

        if (restore.ShouldClearPath)
        {
            _userPreferences =
                _lastDocumentService.Forget(_userPreferences);
            _userPreferencesService.TrySave(_userPreferences);
        }

        LoadIntoEditor(welcomeSource, path: null);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        _viewModel.UpdateCaret(SourceEditor.TextArea.Caret.Line, SourceEditor.TextArea.Caret.Column);
        QueueInspectorCaretSynchronization();
    }

    private void OnNewClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmCanLeaveCurrentDocument())
        {
            return;
        }

        LoadIntoEditor(_welcomeSvgProvider.Load(), path: null);
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Open SVG or text file",
            Filter = "SVG and text files (*.svg;*.txt)|*.svg;*.txt|SVG files (*.svg)|*.svg|Text files (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true && ConfirmCanLeaveCurrentDocument())
        {
            OpenDocument(dialog.FileName);
        }
    }

    private bool OpenDocument(string path)
    {
        if (!IsSupportedFile(path))
        {
            MessageBox.Show(
                this,
                "SvgLiveEditor can open only .svg and .txt files.",
                "Unsupported file",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        try
        {
            string text = _fileService.ReadAllText(path);
            string fullPath = Path.GetFullPath(path);
            LoadIntoEditor(text, fullPath);
            RememberLastDocument(fullPath);
            return true;
        }
        catch (DecoderFallbackException)
        {
            MessageBox.Show(
                this,
                "The selected file is not valid UTF-8.",
                "Cannot open file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"The file could not be opened: {exception.Message}",
                "Cannot open file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveDocument();

    private void OnSaveAsClick(object sender, RoutedEventArgs e) => SaveDocumentAs();

    private bool SaveDocument()
    {
        return string.IsNullOrWhiteSpace(_viewModel.CurrentFilePath)
            ? SaveDocumentAs()
            : SaveToPath(_viewModel.CurrentFilePath);
    }

    private bool SaveDocumentAs()
    {
        SaveFileDialog dialog = new()
        {
            Title = "Save SVG source",
            Filter = "SVG files (*.svg)|*.svg|Text files (*.txt)|*.txt",
            DefaultExt = ".svg",
            AddExtension = true,
            FileName = Path.GetFileNameWithoutExtension(_viewModel.CurrentFileName)
        };

        return dialog.ShowDialog(this) == true && SaveToPath(dialog.FileName);
    }

    private bool SaveToPath(string path)
    {
        try
        {
            _fileService.WriteAllText(path, SourceEditor.Text);
            string fullPath = Path.GetFullPath(path);
            _viewModel.MarkSaved(fullPath);
            RememberLastDocument(fullPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"The file could not be saved: {exception.Message}",
                "Cannot save file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private bool ConfirmCanLeaveCurrentDocument()
    {
        if (!_viewModel.IsModified)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"Save changes to {_viewModel.CurrentFileName} before continuing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        UnsavedChangesChoice choice = result switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel
        };
        bool saveSucceeded = choice == UnsavedChangesChoice.Save && SaveDocument();
        return _unsavedChangesPolicy.CanProceed(_viewModel.IsModified, choice, saveSucceeded);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmCanLeaveCurrentDocument())
        {
            e.Cancel = true;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        AboutWindow dialog = new(
            _applicationInfoService.Create(typeof(App).Assembly))
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (SourceEditor.CanUndo)
        {
            SourceEditor.Undo();
        }
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        if (SourceEditor.CanRedo)
        {
            SourceEditor.Redo();
        }
    }

    private void OnFindClick(object sender, RoutedEventArgs e) => ShowFindPanel(showReplace: false);

    private void OnReplaceClick(object sender, RoutedEventArgs e) => ShowFindPanel(showReplace: true);

    private void ShowFindPanel(bool showReplace)
    {
        FindReplacePanel.Visibility = Visibility.Visible;
        ReplaceControlsPanel.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
        if (SourceEditor.SelectionLength > 0 && !SourceEditor.SelectedText.Contains('\n'))
        {
            FindTextBox.Text = SourceEditor.SelectedText;
        }

        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void OnCloseFindClick(object sender, RoutedEventArgs e)
    {
        FindReplacePanel.Visibility = Visibility.Collapsed;
        SourceEditor.Focus();
    }

    private void OnFindNextClick(object sender, RoutedEventArgs e) => FindNext();

    private void OnFindTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            OnCloseFindClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private bool FindNext()
    {
        string query = FindTextBox.Text;
        if (query.Length == 0)
        {
            return false;
        }

        StringComparison comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int startOffset = Math.Min(SourceEditor.SelectionStart + SourceEditor.SelectionLength, SourceEditor.Text.Length);
        int index = SourceEditor.Text.IndexOf(query, startOffset, comparison);
        if (index < 0 && startOffset > 0)
        {
            index = SourceEditor.Text.IndexOf(query, 0, comparison);
        }

        if (index < 0)
        {
            return false;
        }

        SourceEditor.Select(index, query.Length);
        SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(index).LineNumber);
        SourceEditor.Focus();
        return true;
    }

    private void OnReplaceOneClick(object sender, RoutedEventArgs e)
    {
        string query = FindTextBox.Text;
        StringComparison comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (query.Length > 0 && SourceEditor.SelectedText.Equals(query, comparison))
        {
            int replacementStart = SourceEditor.SelectionStart;
            SourceEditor.Document.Replace(replacementStart, SourceEditor.SelectionLength, ReplaceTextBox.Text);
            SourceEditor.Select(replacementStart, ReplaceTextBox.Text.Length);
        }

        FindNext();
    }

    private void OnReplaceAllClick(object sender, RoutedEventArgs e)
    {
        string query = FindTextBox.Text;
        if (query.Length == 0)
        {
            return;
        }

        StringComparison comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int count = CountOccurrences(SourceEditor.Text, query, comparison);
        if (count == 0)
        {
            return;
        }

        SourceEditor.Text = SourceEditor.Text.Replace(query, ReplaceTextBox.Text, comparison);
        MessageBox.Show(this, $"Replaced {count} occurrence(s).", "Replace All", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static int CountOccurrences(string text, string query, StringComparison comparison)
    {
        int count = 0;
        int offset = 0;
        while (offset <= text.Length - query.Length)
        {
            int index = text.IndexOf(query, offset, comparison);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + query.Length;
        }

        return count;
    }

    private async void OnRefreshPreviewClick(object sender, RoutedEventArgs e)
    {
        if (await EnsureWebViewReadyAsync())
        {
            await RefreshPreviewNowAsync();
        }
    }

    private void OnPanModeClick(object sender, RoutedEventArgs e)
    {
        SetPanMode(PanModeButton.IsChecked == true);
    }

    private void SetPanMode(bool enabled, bool announce = true)
    {
        _isPanModeEnabled = enabled;
        PanModeButton.IsChecked = enabled;
        PanModeButton.ToolTip = enabled
            ? "Pan mode active; left drag an overflowing preview (Escape exits)"
            : "Pan overflowing preview with left drag (H toggles, Escape exits)";
        TryUpdatePreviewPanModeInPlace();
        if (announce)
        {
            _viewModel.SetOperationStatus(
                enabled ? "Pan mode enabled" : "Pan mode disabled");
        }
    }

    private bool TryUpdatePreviewPanModeInPlace()
    {
        if (!_isWebViewReady
            || _activePreviewBridgeToken is not string bridgeToken
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core)
        {
            return false;
        }

        try
        {
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildPanStateMessage(
                    bridgeToken,
                    _isPanModeEnabled));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Re-sending the current state asks the trusted page to terminate any
        // active pointer capture without changing whether Pan mode is enabled.
        TryUpdatePreviewPanModeInPlace();
    }

    private void OnPreviewWebViewLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        TryUpdatePreviewPanModeInPlace();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ApplyPreviewZoomState(_previewZoomCalculator.ZoomIn(
            _previewZoomState,
            GetFitScale()));
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ApplyPreviewZoomState(_previewZoomCalculator.ZoomOut(
            _previewZoomState,
            GetFitScale()));
    }

    private void OnResetZoomClick(object sender, RoutedEventArgs e)
    {
        _previewViewport = PreviewViewportPosition.Center;
        ApplyPreviewZoomState(_previewZoomCalculator.Reset());
    }

    private void OnFitPreviewClick(object sender, RoutedEventArgs e)
    {
        _previewViewport = PreviewViewportPosition.Center;
        ApplyPreviewZoomState(_previewZoomCalculator.Fit());
    }

    private void ApplyPreviewZoomState(PreviewZoomState state)
    {
        PreviewUpdateDecision decision = _previewUpdatePolicy.Decide(
            PreviewUpdateKind.Zoom,
            _hasVisiblePreview);
        _previewZoomState = state;
        _userPreferences = _userPreferences with { PreviewZoom = state };
        _userPreferencesService.TrySave(_userPreferences);
        UpdatePreviewStateText(_previewPresentationState);
        if (_isWebViewReady && !decision.RequiresNavigation)
        {
            TryUpdatePreviewZoomInPlace();
        }
    }

    private bool TryUpdatePreviewZoomInPlace()
    {
        if (!_isWebViewReady
            || !_hasVisiblePreview
            || _isPreviewNavigationRequested
            || _activePreviewNavigationId is not null
            || _activePreviewRevision is not null
            || _activePreviewBridgeToken is not string bridgeToken
            || _lastValidCanvasSize is not SvgCanvasSize canvasSize
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core)
        {
            return false;
        }

        double scale = _previewZoomCalculator.ResolveScale(
            _previewZoomState,
            GetFitScale(canvasSize));
        double renderedWidth = canvasSize.Width * scale;
        double renderedHeight = canvasSize.Height * scale;
        try
        {
            PreviewWebView.ZoomFactor = 1.0;
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildZoomStateMessage(
                    bridgeToken,
                    renderedWidth,
                    renderedHeight,
                    _previewZoomState.Mode == PreviewZoomMode.Manual
                        ? _previewViewport
                        : PreviewViewportPosition.Center));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void OnPreviewWebViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_previewZoomState.Mode != PreviewZoomMode.Fit
            || !_isWebViewReady
            || _lastValidSvg is null)
        {
            return;
        }

        _fitResizeTimer.Stop();
        _fitResizeTimer.Start();
    }

    private void OnPreviewWebViewZoomFactorChanged(object sender, object e)
    {
        // Native document zoom would scale the checkerboard and host HTML. Keep it
        // pinned while the CSP-authorized DOM handler performs artwork-only zoom.
        if (Math.Abs(PreviewWebView.ZoomFactor - 1.0) > 0.0001)
        {
            PreviewWebView.ZoomFactor = 1.0;
        }
    }

    private void OnFitResizeTimerTick(object? sender, EventArgs e)
    {
        _fitResizeTimer.Stop();
        if (_previewZoomState.Mode == PreviewZoomMode.Fit && _isWebViewReady)
        {
            TryUpdatePreviewZoomInPlace();
        }
    }

    private void OnWordWrapClick(object sender, RoutedEventArgs e)
    {
        ApplyWordWrap(WordWrapMenuItem.IsChecked, persist: true);
    }

    private void OnReopenLastDocumentClick(
        object sender,
        RoutedEventArgs e)
    {
        _userPreferences = _userPreferences with
        {
            ReopenLastDocumentOnStartup =
                ReopenLastDocumentMenuItem.IsChecked
        };
        _userPreferencesService.TrySave(_userPreferences);
    }

    private void RememberLastDocument(string path)
    {
        _userPreferences =
            _lastDocumentService.Remember(_userPreferences, path);
        _userPreferencesService.TrySave(_userPreferences);
    }

    private void ApplyWordWrap(bool enabled, bool persist)
    {
        WordWrapMenuItem.IsChecked = enabled;
        SourceEditor.WordWrap = enabled;
        SourceEditor.HorizontalScrollBarVisibility = enabled
            ? ScrollBarVisibility.Hidden
            : ScrollBarVisibility.Auto;

        if (persist)
        {
            _userPreferences = _userPreferences with { WordWrap = enabled };
            _userPreferencesService.TrySave(_userPreferences);
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool controlOnly = modifiers == ModifierKeys.Control;
        Key pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;

        if (ApplicationShortcutResolver.Resolve(modifiers, pressedKey)
            == ApplicationShortcut.ToggleWordWrap)
        {
            ToggleWordWrap();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && pressedKey == Key.C)
        {
            OnCopyPreviewAsPngClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && pressedKey == Key.C)
        {
            OnCopyEntireSourceClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None
            && pressedKey == Key.H
            && CanUseHostPanShortcut())
        {
            SetPanMode(!_isPanModeEnabled);
            e.Handled = true;
        }
        else if (pressedKey == Key.Escape
            && _isPanModeEnabled
            && !PreviewWebView.IsKeyboardFocusWithin)
        {
            SetPanMode(enabled: false);
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.N)
        {
            OnNewClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.O)
        {
            OnOpenClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.S)
        {
            SaveDocument();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            SaveDocumentAs();
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.F)
        {
            ShowFindPanel(showReplace: false);
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.H)
        {
            ShowFindPanel(showReplace: true);
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.Z && SourceEditor.IsKeyboardFocusWithin)
        {
            OnUndoClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (controlOnly && e.Key == Key.Y && SourceEditor.IsKeyboardFocusWithin)
        {
            OnRedoClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && FindReplacePanel.Visibility == Visibility.Visible)
        {
            OnCloseFindClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private bool CanUseHostPanShortcut()
    {
        return !PreviewWebView.IsKeyboardFocusWithin
            && !SourceEditor.IsKeyboardFocusWithin
            && Keyboard.FocusedElement is not TextBoxBase;
    }

    private void ToggleWordWrap()
    {
        ApplyWordWrap(!WordWrapMenuItem.IsChecked, persist: true);
    }

    private void OnWindowPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetSingleDroppedFile(e.Data, out string? path) && IsSupportedFile(path)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (TryGetSingleDroppedFile(e.Data, out string? path)
            && IsSupportedFile(path)
            && ConfirmCanLeaveCurrentDocument())
        {
            OpenDocument(path);
        }
    }

    private static bool TryGetSingleDroppedFile(IDataObject data, out string path)
    {
        path = string.Empty;
        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return false;
        }

        path = files[0];
        return true;
    }

    private static bool IsSupportedFile(string path)
    {
        string extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void OnRuntimeLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void DetachCoreWebViewEvents()
    {
        if (_configuredCoreWebView is null)
        {
            return;
        }

        _configuredCoreWebView.NavigationStarting -= OnPreviewNavigationStarting;
        _configuredCoreWebView.NavigationCompleted -= OnPreviewNavigationCompleted;
        _configuredCoreWebView.ProcessFailed -= OnPreviewProcessFailed;
        _configuredCoreWebView.WebMessageReceived -= OnPreviewWebMessageReceived;
        _configuredCoreWebView = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceEditor.Document.TextChanged -= OnEditorDocumentTextChanged;
        SourceEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        PreviewWebView.CoreWebView2InitializationCompleted -= OnCoreWebView2InitializationCompleted;
        DisposeDocumentInspector();
        _fitResizeTimer.Stop();
        _fitResizeTimer.Tick -= OnFitResizeTimerTick;
        DetachCoreWebViewEvents();
        _previewDebouncer.Dispose();
        PreviewWebView.Dispose();
        base.OnClosed(e);
    }
}
