using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowPreviewPresentationIntegrationTests
{
    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public async Task RealMainWindow_GroupAuthoringStress_ReadyAttestsCurrentPaintedPage()
    {
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));
            _ = RunRealWindowHarnessAsync(completion);
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            await completion.Task.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private static async Task RunRealWindowHarnessAsync(
        TaskCompletionSource<bool> completion)
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.MainWindow.Tests",
            Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        NavigationRecorder? navigation = null;
        try
        {
            window = new MainWindow(localApplicationData)
            {
                Width = 1280,
                Height = 800,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = true
            };
            TaskCompletionSource<CoreWebView2> coreReady = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.PreviewWebView.CoreWebView2InitializationCompleted +=
                (_, args) =>
                {
                    if (args.IsSuccess
                        && window.PreviewWebView.CoreWebView2 is CoreWebView2 core)
                    {
                        navigation = new NavigationRecorder(core, window);
                        coreReady.TrySetResult(core);
                    }
                    else
                    {
                        coreReady.TrySetException(
                            args.InitializationException
                            ?? new InvalidOperationException(
                                "WebView2 initialization failed."));
                    }
                };

            window.Show();
            window.Activate();
            CoreWebView2 core = await coreReady.Task.WaitAsync(
                TimeSpan.FromSeconds(20));

            string welcome = window.SourceEditor.Text;
            long welcomeRevision = GetSourceRevision(window);
            MainWindowSnapshot initial = await WaitForReadyAsync(
                window,
                core,
                welcome,
                welcomeRevision);
            AssertReadySnapshot(initial, welcome, welcomeRevision);
            int stablePageNavigationCount = navigation!.CompletedCount;

            InvokePrivate(
                window,
                "OnResetZoomClick",
                window,
                new RoutedEventArgs());
            Assert.IsTrue(window.PreviewStateText.Text.StartsWith(
                "100%",
                StringComparison.Ordinal));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            await CreateAndAssertAsync(
                window,
                core,
                SvgCreateDestination.SvgRoot,
                SvgCreateElementKind.Group);
            for (int depth = 0; depth < 3; depth++)
            {
                await CreateAndAssertAsync(
                    window,
                    core,
                    SvgCreateDestination.SelectedContext,
                    SvgCreateElementKind.Group);
            }

            for (int index = 0; index < 6; index++)
            {
                await CreateAndAssertAsync(
                    window,
                    core,
                    SvgCreateDestination.SvgRoot,
                    SvgCreateElementKind.Group);
                await CreateAndAssertAsync(
                    window,
                    core,
                    SvgCreateDestination.SelectedContext,
                    SvgCreateElementKind.Group);
            }

            for (int index = 0; index < 4; index++)
            {
                await CreateAndAssertAsync(
                    window,
                    core,
                    SvgCreateDestination.SvgRoot,
                    SvgCreateElementKind.Group);
            }

            await CreateAndAssertAsync(
                window,
                core,
                SvgCreateDestination.SelectedContext,
                SvgCreateElementKind.Circle);

            MainViewModel viewModel = GetField<MainViewModel>(
                window,
                "_viewModel");
            SvgLayerViewModel[] selectable = EnumerateLayers(
                    viewModel.Inspector.LayerRoots)
                .Take(3)
                .ToArray();
            Assert.IsTrue(selectable.Length >= 2);
            viewModel.Inspector.AcceptLayerSelection(selectable[0]);
            viewModel.Inspector.AcceptLayerSelection(selectable[^1]);
            AssertReadySnapshot(
                await CaptureCurrentAsync(
                    window,
                    core,
                    window.SourceEditor.Text),
                window.SourceEditor.Text,
                GetSourceRevision(window));

            await ReparentShapeIntoGroupAsync(window, core);
            await ReparentGroupIntoGroupAsync(window, core);
            await ReorderLayerAsync(window, core);

            string beforeUndo = window.SourceEditor.Text;
            InvokePrivate(window, "OnUndoClick", window, new RoutedEventArgs());
            Assert.AreNotEqual(beforeUndo, window.SourceEditor.Text);
            MainWindowSnapshot undo = await WaitForReadyAsync(
                window,
                core,
                window.SourceEditor.Text,
                GetSourceRevision(window));
            AssertReadySnapshot(
                undo,
                window.SourceEditor.Text,
                GetSourceRevision(window));

            InvokePrivate(window, "OnRedoClick", window, new RoutedEventArgs());
            MainWindowSnapshot redo = await WaitForReadyAsync(
                window,
                core,
                window.SourceEditor.Text,
                GetSourceRevision(window));
            AssertReadySnapshot(
                redo,
                window.SourceEditor.Text,
                GetSourceRevision(window));
            Assert.AreEqual(beforeUndo, window.SourceEditor.Text);

            // Queue several real authoring edits without waiting so that new
            // valid revisions arrive while image presentation is still active.
            for (int index = 0; index < 24; index++)
            {
                SvgCreateDestination destination = index % 3 == 0
                    ? SvgCreateDestination.SvgRoot
                    : SvgCreateDestination.SelectedContext;
                InvokePrivate(
                    window,
                    "CreateVisualElement",
                    destination,
                    SvgCreateElementKind.Group);
            }
            InvokePrivate(
                window,
                "CreateVisualElement",
                SvgCreateDestination.SelectedContext,
                SvgCreateElementKind.Rectangle);
            string rapidSource = window.SourceEditor.Text;
            long rapidRevision = GetSourceRevision(window);
            MainWindowSnapshot rapid = await WaitForReadyAsync(
                window,
                core,
                rapidSource,
                rapidRevision);
            AssertReadySnapshot(rapid, rapidSource, rapidRevision);

            string lastValidSource = window.SourceEditor.Text;
            long lastValidRevision = GetSourceRevision(window);
            window.SourceEditor.AppendText("<");
            await WaitForValidationAsync(viewModel, isValid: false);
            MainWindowSnapshot invalid = await CaptureCurrentAsync(
                window,
                core,
                lastValidSource);
            AssertReadySnapshot(invalid, lastValidSource, lastValidRevision);
            Assert.AreNotEqual(
                GetSourceRevision(window),
                invalid.VisibleSourceRevision);

            window.SourceEditor.Undo();
            string recoveredSource = window.SourceEditor.Text;
            long recoveredRevision = GetSourceRevision(window);
            MainWindowSnapshot recovered = await WaitForReadyAsync(
                window,
                core,
                recoveredSource,
                recoveredRevision);
            AssertReadySnapshot(
                recovered,
                recoveredSource,
                recoveredRevision);
            Assert.AreEqual(
                initial.ExpectedToken,
                recovered.ExpectedToken,
                "Ordinary edits must retain the same trusted preview document token.");

            navigation!.AssertEveryNavigationCompletedAfterStarting();
            Assert.AreEqual(0, navigation.FailedNavigationCount);
            Assert.AreEqual(
                stablePageNavigationCount,
                navigation.CompletedCount,
                "Ordinary valid source edits must update the isolated image without replacing the trusted page.");
            Assert.AreEqual(1.0, window.PreviewWebView.ZoomFactor, 0.0001);
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            navigation?.Dispose();
            if (window is not null)
            {
                GetField<MainViewModel>(window, "_viewModel")
                    .MarkSaved(string.Empty);
                window.Close();
            }
            // Let WebView2's graphics-capture callbacks drain before the STA
            // dispatcher and test host tear down, matching the established
            // bridge integration harness lifecycle.
            await Task.Delay(500);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                DispatcherPriority.Background);
            TryDeleteDirectory(localApplicationData);
        }
    }

    private static async Task CreateAndAssertAsync(
        MainWindow window,
        CoreWebView2 core,
        SvgCreateDestination destination,
        SvgCreateElementKind kind)
    {
        long previousRevision = GetSourceRevision(window);
        InvokePrivate(window, "CreateVisualElement", destination, kind);
        long expectedRevision = GetSourceRevision(window);
        Assert.IsTrue(expectedRevision > previousRevision);
        string expectedSource = window.SourceEditor.Text;
        MainWindowSnapshot snapshot = await WaitForReadyAsync(
            window,
            core,
            expectedSource,
            expectedRevision);
        AssertReadySnapshot(snapshot, expectedSource, expectedRevision);
    }

    private static async Task ReparentShapeIntoGroupAsync(
        MainWindow window,
        CoreWebView2 core)
    {
        await CreateAndAssertAsync(
            window,
            core,
            SvgCreateDestination.SvgRoot,
            SvgCreateElementKind.Ellipse);
        SvgLayerViewModel shape = GetField<MainViewModel>(window, "_viewModel")
            .Inspector.SelectedLayer!;
        string shapeId = shape.Element.Id!;

        await CreateAndAssertAsync(
            window,
            core,
            SvgCreateDestination.SvgRoot,
            SvgCreateElementKind.Group);
        SvgLayerViewModel group = GetField<MainViewModel>(window, "_viewModel")
            .Inspector.SelectedLayer!;
        string groupId = group.Element.Id!;
        await ApplyLayerMoveAndAssertAsync(
            window,
            core,
            FindLayer(window, shapeId),
            FindLayer(window, groupId),
            SvgLayerDropPlacement.Inside);
    }

    private static async Task ReparentGroupIntoGroupAsync(
        MainWindow window,
        CoreWebView2 core)
    {
        await CreateAndAssertAsync(
            window,
            core,
            SvgCreateDestination.SvgRoot,
            SvgCreateElementKind.Group);
        string sourceId = GetField<MainViewModel>(window, "_viewModel")
            .Inspector.SelectedLayer!.Element.Id!;
        await CreateAndAssertAsync(
            window,
            core,
            SvgCreateDestination.SvgRoot,
            SvgCreateElementKind.Group);
        string targetId = GetField<MainViewModel>(window, "_viewModel")
            .Inspector.SelectedLayer!.Element.Id!;
        await ApplyLayerMoveAndAssertAsync(
            window,
            core,
            FindLayer(window, sourceId),
            FindLayer(window, targetId),
            SvgLayerDropPlacement.Inside);
    }

    private static async Task ApplyLayerMoveAndAssertAsync(
        MainWindow window,
        CoreWebView2 core,
        SvgLayerViewModel source,
        SvgLayerViewModel target,
        SvgLayerDropPlacement placement)
    {
        long previousRevision = GetSourceRevision(window);
        SetField(window, "_layerDragSourceRevision", previousRevision);
        InvokePrivate(window, "ApplyLayerMove", source, target, placement);
        long expectedRevision = GetSourceRevision(window);
        Assert.IsTrue(expectedRevision > previousRevision);
        string expectedSource = window.SourceEditor.Text;
        MainWindowSnapshot snapshot = await WaitForReadyAsync(
            window,
            core,
            expectedSource,
            expectedRevision);
        AssertReadySnapshot(snapshot, expectedSource, expectedRevision);
    }

    private static async Task ReorderLayerAsync(
        MainWindow window,
        CoreWebView2 core)
    {
        MainViewModel viewModel = GetField<MainViewModel>(window, "_viewModel");
        SvgDocumentIndex document = viewModel.Inspector.DocumentIndex!;
        SvgLayerOrderService service = new();
        SvgLayerViewModel? selected = null;
        SvgLayerOrderCommand selectedCommand = default;
        foreach (SvgLayerViewModel layer in EnumerateLayers(
                     viewModel.Inspector.LayerRoots))
        {
            foreach (SvgLayerOrderCommand command in
                     Enum.GetValues<SvgLayerOrderCommand>())
            {
                if (service.GetAvailability(
                        document,
                        layer.Element,
                        command).CanExecute)
                {
                    selected = layer;
                    selectedCommand = command;
                    break;
                }
            }
            if (selected is not null)
            {
                break;
            }
        }

        Assert.IsNotNull(selected);
        viewModel.Inspector.AcceptLayerSelection(selected);
        long previousRevision = GetSourceRevision(window);
        InvokePrivate(window, "ApplyLayerOrder", selectedCommand);
        long expectedRevision = GetSourceRevision(window);
        Assert.IsTrue(expectedRevision > previousRevision);
        string expectedSource = window.SourceEditor.Text;
        MainWindowSnapshot snapshot = await WaitForReadyAsync(
            window,
            core,
            expectedSource,
            expectedRevision);
        AssertReadySnapshot(snapshot, expectedSource, expectedRevision);
    }

    private static async Task<MainWindowSnapshot> WaitForReadyAsync(
        MainWindow window,
        CoreWebView2 core,
        string expectedSource,
        long expectedSourceRevision)
    {
        TaskCompletionSource<MainWindowSnapshot> ready = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DependencyPropertyDescriptor descriptor =
            DependencyPropertyDescriptor.FromProperty(
                System.Windows.Controls.TextBlock.TextProperty,
                typeof(System.Windows.Controls.TextBlock));
        bool captureStarted = false;
        EventHandler? changed = null;
        changed = async (_, _) =>
        {
            if (captureStarted
                || !IsReadyForRevision(window, expectedSourceRevision))
            {
                return;
            }

            captureStarted = true;
            try
            {
                await WaitForWpfRenderAsync();
                ready.TrySetResult(await CaptureCurrentAsync(
                    window,
                    core,
                    expectedSource));
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        };
        descriptor.AddValueChanged(window.PreviewStateText, changed);
        try
        {
            changed(window.PreviewStateText, EventArgs.Empty);
            return await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            descriptor.RemoveValueChanged(window.PreviewStateText, changed);
        }
    }

    private static async Task WaitForWpfRenderAsync()
    {
        for (int frame = 0; frame < 2; frame++)
        {
            TaskCompletionSource<bool> rendered = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                CompositionTarget.Rendering -= handler;
                rendered.TrySetResult(true);
            };
            CompositionTarget.Rendering += handler;
            try
            {
                await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                CompositionTarget.Rendering -= handler;
            }
        }

        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.AreEqual(0, NativeMethods.DwmFlush());
    }

    private static bool IsReadyForRevision(
        MainWindow window,
        long expectedSourceRevision) =>
        window.PreviewStateText.Text.Contains("Ready", StringComparison.Ordinal)
        && GetField<string>(window, "_previewPresentationState") == "Ready"
        && GetField<long?>(window, "_visiblePreviewSourceRevision")
            == expectedSourceRevision;

    private static async Task WaitForValidationAsync(
        MainViewModel viewModel,
        bool isValid)
    {
        if (viewModel.IsSvgValid == isValid)
        {
            return;
        }

        TaskCompletionSource<bool> changed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsSvgValid)
                && viewModel.IsSvgValid == isValid)
            {
                changed.TrySetResult(true);
            }
        };
        viewModel.PropertyChanged += handler;
        try
        {
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            viewModel.PropertyChanged -= handler;
        }
    }

    private static async Task<MainWindowSnapshot> CaptureCurrentAsync(
        MainWindow window,
        CoreWebView2 core,
        string expectedSvg)
    {
        PreviewNavigationCoordinator coordinator =
            GetField<PreviewNavigationCoordinator>(
                window,
                "_previewNavigationCoordinator");
        PreviewRenderRequest? active = GetField<PreviewRenderRequest?>(
            coordinator,
            "_active");
        PreviewRenderRequest? pending = GetField<PreviewRenderRequest?>(
            coordinator,
            "_pending");
        PreviewRenderRequest? lastSuccessful =
            GetField<PreviewRenderRequest?>(
                coordinator,
                "_lastSuccessful");
        long requestedRevision = GetField<long>(coordinator, "_nextRevision");
        string expectedImageSource = "data:image/svg+xml;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(expectedSvg));
        string expectedImageJson = JsonSerializer.Serialize(expectedImageSource);
        PixelEvidence screenPixels = AnalyzeScreenPixels(
            window.PreviewWebView);
        int naturalPresentationFrames = 0;
        Stopwatch screenPresentationWait = Stopwatch.StartNew();
        while (!screenPixels.HasSubstantialArtwork
               && screenPresentationWait.Elapsed < TimeSpan.FromSeconds(5))
        {
            await WaitForWpfRenderAsync();
            naturalPresentationFrames += 2;
            screenPixels = AnalyzeScreenPixels(window.PreviewWebView);
        }
        string script = $$"""
            (() => {
              const image = document.querySelector('img');
              const viewport = document.querySelector('.preview-viewport');
              const style = image ? getComputedStyle(image) : null;
              const rect = image ? image.getBoundingClientRect() : null;
              const bodyStyle = document.body
                ? getComputedStyle(document.body)
                : null;
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
                documentExists: !!document.documentElement,
                hostScriptReady:
                  document.body?.dataset.hostScriptReady === 'true',
                checkerboardExists: !!viewport &&
                  bodyStyle?.backgroundImage !== 'none',
                bodyClientWidth: document.body?.clientWidth || 0,
                bodyClientHeight: document.body?.clientHeight || 0,
                documentScrollWidth: document.documentElement?.scrollWidth || 0,
                documentScrollHeight: document.documentElement?.scrollHeight || 0,
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
        MainWindowDomSnapshot dom =
            JsonSerializer.Deserialize<MainWindowDomSnapshot>(
                result,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidOperationException(
                "WebView2 returned no DOM state.");

        using MemoryStream preview = new();
        await core.CapturePreviewAsync(
            CoreWebView2CapturePreviewImageFormat.Png,
            preview);
        PixelEvidence capturedPixels = AnalyzeCapturedPixels(preview);
        string? token = GetField<string?>(
            window,
            "_activePreviewBridgeToken");
        return new MainWindowSnapshot(
            GetSourceRevision(window),
            requestedRevision,
            active?.Revision,
            pending?.Revision,
            lastSuccessful?.Revision,
            GetField<long?>(window, "_activePreviewRevision"),
            GetField<long?>(window, "_activePreviewSourceRevision"),
            GetField<long?>(window, "_visiblePreviewSourceRevision"),
            token ?? string.Empty,
            GetField<string>(window, "_previewPresentationState"),
            GetField<bool>(window, "_hasVisiblePreview"),
            core.Source,
            window.PreviewWebView.Visibility,
            window.PreviewWebView.IsVisible,
            window.IsActive,
            window.WindowState,
            window.PreviewWebView.IsKeyboardFocusWithin,
            window.PreviewWebView.ActualWidth,
            window.PreviewWebView.ActualHeight,
            window.PreviewWebView.ZoomFactor,
            window.PreviewMessagePanel.Visibility,
            capturedPixels,
            screenPixels,
            naturalPresentationFrames,
            dom);
    }

    private static void AssertReadySnapshot(
        MainWindowSnapshot snapshot,
        string expectedSvg,
        long expectedSourceRevision)
    {
        Console.WriteLine(
            $"Ready hostSource={snapshot.HostSourceRevision} "
            + $"visibleSource={snapshot.VisibleSourceRevision} "
            + $"requestedRender={snapshot.RequestedRenderRevision} "
            + $"successfulRender={snapshot.LastSuccessfulRenderRevision} "
            + $"activeRender={snapshot.ActiveRenderRevision} "
            + $"pendingRender={snapshot.CoordinatorPendingRevision} "
            + $"coreSource={snapshot.CoreSource} "
            + $"location={snapshot.Dom.Location} "
            + $"trusted={snapshot.Dom.HostScriptReady} "
            + $"checkerboard={snapshot.Dom.CheckerboardExists} "
            + $"image={snapshot.Dom.ImageExists}/"
            + $"{snapshot.Dom.ImageComplete}/"
            + $"{snapshot.Dom.NaturalWidth}x{snapshot.Dom.NaturalHeight}/"
            + $"{snapshot.Dom.RenderedWidth}x{snapshot.Dom.RenderedHeight} "
            + $"load={snapshot.Dom.LoadEvent} "
            + $"presented={snapshot.Dom.Presented} "
            + $"wpf={snapshot.WebViewVisibility}/"
            + $"active={snapshot.WindowIsActive}/"
            + $"state={snapshot.WindowState}/"
            + $"focus={snapshot.WebViewHasKeyboardFocus}/"
            + $"{snapshot.WebViewWidth}x{snapshot.WebViewHeight}/"
            + $"zoom={snapshot.WebViewZoomFactor}/"
            + $"browserPixels={snapshot.CapturedPixels.DistinctColors}/"
            + $"{snapshot.CapturedPixels.ArtworkSamples}/"
            + $"{snapshot.CapturedPixels.TotalSamples} "
            + $"screenPixels={snapshot.ScreenPixels.DistinctColors}/"
            + $"{snapshot.ScreenPixels.ArtworkSamples}/"
            + $"{snapshot.ScreenPixels.TotalSamples}/"
            + $"frames={snapshot.NaturalPresentationFrames} "
            + $"overlay={snapshot.MessagePanelVisibility}");
        Assert.IsTrue(expectedSvg.Length > 0);
        Assert.AreEqual("Ready", snapshot.PresentationState);
        Assert.IsTrue(snapshot.HasVisiblePreview);
        Assert.AreEqual(expectedSourceRevision, snapshot.VisibleSourceRevision);
        Assert.IsNull(snapshot.ActiveRenderRevision);
        Assert.IsNull(snapshot.ActiveSourceRevision);
        Assert.IsNull(snapshot.CoordinatorActiveRevision);
        Assert.IsNull(snapshot.CoordinatorPendingRevision);
        Assert.AreEqual(
            snapshot.RequestedRenderRevision,
            snapshot.LastSuccessfulRenderRevision,
            "Ready must attest the latest requested render.");
        Assert.AreEqual(snapshot.ExpectedToken, snapshot.Dom.PageToken);
        Assert.AreEqual(expectedSourceRevision, snapshot.Dom.PageSourceRevision);
        Assert.AreEqual("about:blank", snapshot.CoreSource);
        Assert.AreEqual("about:blank", snapshot.Dom.Location);
        Assert.IsTrue(snapshot.Dom.DocumentExists);
        Assert.IsTrue(snapshot.Dom.HostScriptReady);
        Assert.IsTrue(snapshot.Dom.CheckerboardExists);
        Assert.IsTrue(snapshot.Dom.BodyClientWidth > 0);
        Assert.IsTrue(snapshot.Dom.BodyClientHeight > 0);
        Assert.IsTrue(snapshot.Dom.DocumentScrollWidth > 0);
        Assert.IsTrue(snapshot.Dom.DocumentScrollHeight > 0);
        Assert.IsTrue(snapshot.Dom.ImageExists);
        Assert.IsTrue(snapshot.Dom.ImageSourcePresent);
        Assert.IsTrue(snapshot.Dom.ImageSourceCurrent);
        Assert.IsTrue(snapshot.Dom.ImageComplete);
        Assert.IsTrue(snapshot.Dom.NaturalWidth > 0);
        Assert.IsTrue(snapshot.Dom.NaturalHeight > 0);
        Assert.IsTrue(snapshot.Dom.RenderedWidth > 0);
        Assert.IsTrue(snapshot.Dom.RenderedHeight > 0);
        Assert.AreEqual("block", snapshot.Dom.Display);
        Assert.AreEqual("visible", snapshot.Dom.Visibility);
        Assert.AreEqual("1", snapshot.Dom.Opacity);
        Assert.IsTrue(snapshot.Dom.LoadEvent is "load" or "complete-before-listener");
        Assert.IsTrue(snapshot.Dom.Presented);
        Assert.IsTrue(snapshot.Dom.CanvasDrawSucceeded);
        Assert.AreEqual(Visibility.Visible, snapshot.WebViewVisibility);
        Assert.IsTrue(snapshot.WebViewIsVisible);
        Assert.IsTrue(snapshot.WebViewWidth > 0);
        Assert.IsTrue(snapshot.WebViewHeight > 0);
        Assert.AreEqual(1.0, snapshot.WebViewZoomFactor, 0.0001);
        Assert.AreEqual(Visibility.Collapsed, snapshot.MessagePanelVisibility);
        Assert.IsTrue(
            snapshot.CapturedPixels.DistinctColors > 1,
            "The captured WebView frame must not be a single blank color.");
        Assert.IsTrue(
            snapshot.ScreenPixels.DistinctColors > 8,
            "Ready must not precede a visibly nonblank on-screen Preview frame.");
        Assert.IsTrue(
            snapshot.CapturedPixels.HasSubstantialArtwork,
            "Browser CapturePreview must contain substantial SVG artwork, not only the checkerboard.");
        Assert.IsTrue(
            snapshot.ScreenPixels.HasSubstantialArtwork,
            "The actual CompositionControl screen region must contain substantial SVG artwork, not only the checkerboard.");
    }

    private static PixelEvidence AnalyzeCapturedPixels(MemoryStream png)
    {
        png.Position = 0;
        PngBitmapDecoder decoder = new(
            png,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        FormatConvertedBitmap converted = new(
            source,
            PixelFormats.Bgra32,
            null,
            0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        HashSet<uint> colors = [];
        int artworkSamples = 0;
        int totalSamples = 0;
        int xStep = Math.Max(1, converted.PixelWidth / 160);
        int yStep = Math.Max(1, converted.PixelHeight / 160);
        for (int y = 0; y < converted.PixelHeight; y += yStep)
        {
            for (int x = 0; x < converted.PixelWidth; x += xStep)
            {
                int offset = (y * stride) + (x * 4);
                uint color = BitConverter.ToUInt32(pixels, offset);
                colors.Add(color);
                totalSamples++;
                if (IsArtworkPixel(
                        pixels[offset + 2],
                        pixels[offset + 1],
                        pixels[offset],
                        pixels[offset + 3]))
                {
                    artworkSamples++;
                }
            }
        }

        return new PixelEvidence(colors.Count, artworkSamples, totalSamples);
    }

    private static PixelEvidence AnalyzeScreenPixels(FrameworkElement element)
    {
        System.Windows.Point topLeft = element.PointToScreen(
            new System.Windows.Point(0, 0));
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        int width = Math.Max(
            1,
            (int)Math.Floor(element.ActualWidth * dpi.DpiScaleX));
        int height = Math.Max(
            1,
            (int)Math.Floor(element.ActualHeight * dpi.DpiScaleY));
        using System.Drawing.Bitmap bitmap = new(width, height);
        using (System.Drawing.Graphics graphics =
            System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                (int)Math.Round(topLeft.X),
                (int)Math.Round(topLeft.Y),
                0,
                0,
                new System.Drawing.Size(width, height));
        }

        HashSet<int> colors = [];
        int artworkSamples = 0;
        int totalSamples = 0;
        int xStep = Math.Max(1, width / 160);
        int yStep = Math.Max(1, height / 160);
        for (int y = 0; y < height; y += yStep)
        {
            for (int x = 0; x < width; x += xStep)
            {
                System.Drawing.Color color = bitmap.GetPixel(x, y);
                colors.Add(color.ToArgb());
                totalSamples++;
                if (IsArtworkPixel(
                        color.R,
                        color.G,
                        color.B,
                        color.A))
                {
                    artworkSamples++;
                }
            }
        }

        return new PixelEvidence(colors.Count, artworkSamples, totalSamples);
    }

    private static bool IsArtworkPixel(byte red, byte green, byte blue, byte alpha)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return alpha >= 128 && maximum - minimum >= 24;
    }

    private static SvgLayerViewModel FindLayer(
        MainWindow window,
        string elementId) =>
        EnumerateLayers(
                GetField<MainViewModel>(window, "_viewModel")
                    .Inspector.LayerRoots)
            .Single(layer => layer.Element.Id == elementId);

    private static IEnumerable<SvgLayerViewModel> EnumerateLayers(
        IEnumerable<SvgLayerViewModel> roots)
    {
        foreach (SvgLayerViewModel root in roots)
        {
            yield return root;
            foreach (SvgLayerViewModel child in EnumerateLayers(root.Children))
            {
                yield return child;
            }
        }
    }

    private static long GetSourceRevision(MainWindow window) =>
        GetField<SourceRevisionTracker>(window, "_sourceRevisionTracker")
            .Current;

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target)!;
    }

    private static void SetField<T>(object target, string name, T value)
    {
        FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static object? InvokePrivate(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length)
            ?? throw new MissingMethodException(
                target.GetType().Name,
                methodName);
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class NavigationRecorder : IDisposable
    {
        private readonly CoreWebView2 _core;
        private readonly MainWindow _window;
        private readonly HashSet<ulong> _started = [];
        private readonly HashSet<ulong> _completed = [];

        public NavigationRecorder(CoreWebView2 core, MainWindow window)
        {
            _core = core;
            _window = window;
            _core.NavigationStarting += OnStarting;
            _core.NavigationCompleted += OnCompleted;
        }

        public int FailedNavigationCount { get; private set; }

        public int CompletedCount => _completed.Count;

        private void OnStarting(
            object? sender,
            CoreWebView2NavigationStartingEventArgs args)
        {
            _started.Add(args.NavigationId);
            string uri = args.Uri.Length <= 32
                ? args.Uri
                : args.Uri[..32] + "...";
            Console.WriteLine(
                $"NavigationStarting id={args.NavigationId} uri={uri} "
                + $"activeRender={GetField<long?>(_window, "_activePreviewRevision")} "
                + $"activeSource={GetField<long?>(_window, "_activePreviewSourceRevision")}");
        }

        private void OnCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args)
        {
            _completed.Add(args.NavigationId);
            if (!args.IsSuccess)
            {
                FailedNavigationCount++;
            }
            Console.WriteLine(
                $"NavigationCompleted id={args.NavigationId} success={args.IsSuccess} "
                + $"error={args.WebErrorStatus} "
                + $"presentation={GetField<string>(_window, "_previewPresentationState")}");
        }

        public void AssertEveryNavigationCompletedAfterStarting()
        {
            foreach (ulong navigationId in _completed)
            {
                Assert.IsTrue(
                    _started.Contains(navigationId),
                    $"Navigation {navigationId} completed without a recorded start.");
            }
        }

        public void Dispose()
        {
            _core.NavigationStarting -= OnStarting;
            _core.NavigationCompleted -= OnCompleted;
        }
    }

    private sealed record MainWindowSnapshot(
        long HostSourceRevision,
        long RequestedRenderRevision,
        long? CoordinatorActiveRevision,
        long? CoordinatorPendingRevision,
        long? LastSuccessfulRenderRevision,
        long? ActiveRenderRevision,
        long? ActiveSourceRevision,
        long? VisibleSourceRevision,
        string ExpectedToken,
        string PresentationState,
        bool HasVisiblePreview,
        string CoreSource,
        Visibility WebViewVisibility,
        bool WebViewIsVisible,
        bool WindowIsActive,
        WindowState WindowState,
        bool WebViewHasKeyboardFocus,
        double WebViewWidth,
        double WebViewHeight,
        double WebViewZoomFactor,
        Visibility MessagePanelVisibility,
        PixelEvidence CapturedPixels,
        PixelEvidence ScreenPixels,
        int NaturalPresentationFrames,
        MainWindowDomSnapshot Dom);

    private sealed record PixelEvidence(
        int DistinctColors,
        int ArtworkSamples,
        int TotalSamples)
    {
        public bool HasSubstantialArtwork =>
            TotalSamples > 0
            && ArtworkSamples >= Math.Max(250, TotalSamples / 25);
    }

    private sealed record MainWindowDomSnapshot(
        string Location,
        bool DocumentExists,
        bool HostScriptReady,
        bool CheckerboardExists,
        double BodyClientWidth,
        double BodyClientHeight,
        double DocumentScrollWidth,
        double DocumentScrollHeight,
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
        string Display,
        string Visibility,
        string Opacity,
        string LoadEvent,
        bool Presented,
        bool CanvasDrawSucceeded);

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        internal static extern int DwmFlush();
    }
}
