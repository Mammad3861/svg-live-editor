using System.IO;
using System.Windows;
using Microsoft.Win32;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private sealed record PngExportSnapshot(long Session, long Revision, string Source,
        string? DocumentPath, string Token, SvgCanvasSize CanvasSize);

    private readonly PngExportFileService _pngExportFileService = new();
    private bool _isChoosingPngExport;
    private CancellationTokenSource? _pngExportCancellation;
    private TaskCompletionSource<PreviewPngPayload?>? _pngExportCompletion;
    private PngExportSnapshot? _pngExportSnapshot;

    private bool HasPendingExportText() => _previewTextEditSession is not null
        || _pendingPreviewTextEditCommit is not null || _isInspectorTextCompositionActive
        || _viewModel.Inspector.TextContent is { HasUncommittedValue: true };

    private bool TryCapturePngExport(out PngExportSnapshot? snapshot)
    {
        snapshot = null;
        if (_isWindowClosing || HasPendingExportText() || !_isWebViewReady || !_hasVisiblePreview
            || _previewPresentationState != "Ready" || _previewPngSourceState != PreviewPngSourceState.CurrentValid
            || _visiblePreviewSourceRevision != _sourceRevisionTracker.Current
            || _activePreviewBridgeToken is not string token || _lastValidCanvasSize is not SvgCanvasSize canvas)
            return false;
        if (!_previewPngCopyPolicy.TryCreatePlan(true, PreviewPngSourceState.CurrentValid, canvas, out _))
            return false;
        snapshot = new(_documentSession, _sourceRevisionTracker.Current, SourceEditor.Text,
            _viewModel.CurrentFilePath, token, canvas);
        return true;
    }

    private bool IsPngExportCurrent(PngExportSnapshot snapshot) =>
        TryCapturePngExport(out PngExportSnapshot? current) && current == snapshot;

    private async void OnExportPngClick(object sender, RoutedEventArgs e)
    {
        if (_isChoosingPngExport || _pngExportCancellation is not null || _pendingPreviewPngRequest is not null)
        {
            _viewModel.SetOperationStatus("An image operation is already in progress.");
            return;
        }
        if (!TryCapturePngExport(out PngExportSnapshot? snapshot))
        {
            _viewModel.SetOperationStatus("Finish or cancel pending text edits and wait for the current valid Preview before exporting PNG.");
            return;
        }
        _isChoosingPngExport = true;
        try
        {
            ExportPngWindow dimensions = new(snapshot!.CanvasSize) { Owner = this };
            if (dimensions.ShowDialog() != true) return;
            SaveFileDialog file = new()
            {
                Title = "Export PNG", Filter = "PNG image (*.png)|*.png", DefaultExt = ".png",
                AddExtension = true, OverwritePrompt = true,
                FileName = Path.GetFileNameWithoutExtension(_viewModel.CurrentFileName) + ".png"
            };
            if (file.ShowDialog(this) != true) return;
            if (!IsPngExportCurrent(snapshot))
            {
                _viewModel.SetOperationStatus("The document changed. Start PNG export again.");
                return;
            }
            await ExportPngToFileAsync(file.FileName, dimensions.Dimensions, CancellationToken.None);
        }
        finally { _isChoosingPngExport = false; }
    }

    private async Task<bool> ExportPngToFileAsync(string path, PreviewPngSize size, CancellationToken cancellationToken)
    {
        if (_pngExportCancellation is not null || _pendingPreviewPngRequest is not null
            || !TryCapturePngExport(out PngExportSnapshot? snapshot))
        {
            _viewModel.SetOperationStatus("PNG export requires the current valid Preview with no pending text edit or image operation.");
            return false;
        }
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pngExportCancellation = cancellation;
        _pngExportSnapshot = snapshot;
        _pngExportCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _ = PreviewPngSizeCalculator.FitExport(snapshot!.CanvasSize, size);
            PngExportDestination destination = _pngExportFileService.CaptureDestination(path, snapshot.DocumentPath);
            cancellation.Token.ThrowIfCancellationRequested();
            StartPreviewPngRequest(PreviewPngRequestPurpose.FileExport, exportSize: size);
            if (_pendingPreviewPngRequest?.Purpose != PreviewPngRequestPurpose.FileExport)
                throw new InvalidOperationException("The PNG request could not be started.");
            _viewModel.SetOperationStatus("Exporting PNG... Press Escape to cancel.");
            PreviewPngPayload? payload = await _pngExportCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation.Token);
            if (payload is null) throw new InvalidOperationException("PNG export was canceled, timed out, or failed validation.");
            using PreparedPngExport prepared = await Task.Run(() =>
                _pngExportFileService.Prepare(destination, payload, snapshot.DocumentPath, cancellation.Token));
            prepared.Commit(() => !cancellation.IsCancellationRequested && IsPngExportCurrent(snapshot));
            _viewModel.SetOperationStatus($"PNG exported · {size.Width} × {size.Height} · {destination.Path}");
            return true;
        }
        catch (OperationCanceledException)
        {
            _viewModel.SetOperationStatus("PNG export canceled or the document changed. No destination was replaced.");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or InvalidOperationException or TimeoutException)
        {
            _viewModel.SetOperationStatus($"Could not export PNG: {exception.Message}");
            return false;
        }
        finally
        {
            if (_pendingPreviewPngRequest?.Purpose == PreviewPngRequestPurpose.FileExport)
                CancelPendingPreviewPngRequest();
            _pngExportCompletion = null;
            _pngExportSnapshot = null;
            _pngExportCancellation = null;
        }
    }

    private void CancelPngExport()
    {
        _pngExportCancellation?.Cancel();
        if (_pendingPreviewPngRequest?.Purpose == PreviewPngRequestPurpose.FileExport)
            CancelPendingPreviewPngRequest();
    }
}
