using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private const uint GetWheelScrollCharacters = 0x006C;
    private const uint DefaultHorizontalScrollCharacters = 3;

    private readonly NativeHorizontalWheelMessageParser
        _nativeHorizontalWheelMessageParser = new();
    private readonly PreviewNativeHorizontalScrollPolicy
        _previewNativeHorizontalScrollPolicy = new();
    private readonly PreviewScreenHitTester _previewScreenHitTester = new();
    private HwndSource? _mainWindowHwndSource;
    private bool _isWindowClosing;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint handle = new WindowInteropHelper(this).Handle;
        HwndSource? source = HwndSource.FromHwnd(handle);
        if (source is null || ReferenceEquals(_mainWindowHwndSource, source))
        {
            return;
        }

        DetachNativePreviewInputHook();
        source.AddHook(OnMainWindowMessage);
        _mainWindowHwndSource = source;
    }

    private nint OnMainWindowMessage(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (!_nativeHorizontalWheelMessageParser.TryParse(
                message,
                wParam,
                lParam,
                out NativeHorizontalWheelInput input)
            || !TryIsPointerOverReadyPreview(input.ScreenPoint)
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core
            || !ReferenceEquals(_configuredCoreWebView, core)
            || !_previewNavigationPolicy.IsTrustedWebMessageSource(core.Source))
        {
            return nint.Zero;
        }

        PreviewNativeScrollContext context = new(
            GetNativePreviewInputState(),
            _isPreviewNavigationRequested
                || _activePreviewNavigationId is not null
                || _activePreviewRevision is not null,
            IsPointerOverPreview: true,
            _activePreviewBridgeToken,
            PreviewWebView.ActualWidth);
        if (!_previewNativeHorizontalScrollPolicy.TryCreateRequest(
                input,
                context,
                GetHorizontalScrollCharacters(),
                out PreviewNativeScrollRequest request))
        {
            return nint.Zero;
        }

        try
        {
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildHorizontalScrollMessage(
                    request.NavigationToken,
                    request.DeltaX));
            // This exact native message is now owned by the trusted page route.
            // Stopping WPF propagation prevents a second DOM application.
            handled = true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidOperationException
            or COMException)
        {
            // A navigation/disposal race invalidates the current token and input.
        }

        return nint.Zero;
    }

    private PreviewNativeInputState GetNativePreviewInputState()
    {
        if (_isWindowClosing)
        {
            return PreviewNativeInputState.Disposed;
        }
        if (!_isWebViewReady || !_hasVisiblePreview)
        {
            return _previewPresentationState == "Error"
                ? PreviewNativeInputState.Error
                : PreviewNativeInputState.Loading;
        }
        return PreviewNativeInputState.Ready;
    }

    private bool TryIsPointerOverReadyPreview(NativeScreenPoint pointer)
    {
        if (_isWindowClosing
            || !IsActive
            || !IsEnabled
            || !PreviewWebView.IsLoaded
            || !PreviewWebView.IsVisible
            || !PreviewWebView.IsEnabled
            || _previewContextMenu.IsOpen
            || FileDropOverlay.Visibility == Visibility.Visible
            || PreviewWebView.Visibility != Visibility.Visible
            || PreviewWebView.ActualWidth <= 0
            || PreviewWebView.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            Point topLeft = PreviewWebView.PointToScreen(new Point(0, 0));
            DpiScale dpi = VisualTreeHelper.GetDpi(PreviewWebView);
            return _previewScreenHitTester.Contains(
                pointer,
                topLeft.X,
                topLeft.Y,
                PreviewWebView.ActualWidth,
                PreviewWebView.ActualHeight,
                dpi.DpiScaleX,
                dpi.DpiScaleY);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static uint GetHorizontalScrollCharacters()
    {
        return SystemParametersInfo(
            GetWheelScrollCharacters,
            0,
            out uint scrollCharacters,
            0)
            ? scrollCharacters
            : DefaultHorizontalScrollCharacters;
    }

    private void DetachNativePreviewInputHook()
    {
        if (_mainWindowHwndSource is not HwndSource source)
        {
            return;
        }

        try
        {
            source.RemoveHook(OnMainWindowMessage);
        }
        catch (InvalidOperationException)
        {
            // The HWND source can already be disposed during application teardown.
        }
        finally
        {
            _mainWindowHwndSource = null;
        }
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        out uint value,
        uint update);
}
