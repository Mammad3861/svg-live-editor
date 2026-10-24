using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed record PngExportDestination(
    string Path, long? Length, DateTime LastWriteUtc, DateTime CreationUtc);

public sealed class PngExportFileService
{
    public PngExportDestination CaptureDestination(string path, string? documentPath)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Choose a full PNG file path.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        if (!Path.GetExtension(fullPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The export filename must end in .png.", nameof(path));
        if (!string.IsNullOrWhiteSpace(documentPath)
            && fullPath.Equals(Path.GetFullPath(documentPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Export cannot overwrite the current document.", nameof(path));

        DirectoryInfo? directory = new(Path.GetDirectoryName(fullPath)!);
        while (directory is not null)
        {
            if (!directory.Exists) throw new DirectoryNotFoundException("The export folder no longer exists.");
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Choose an export folder without redirected folders.");
            directory = directory.Parent;
        }
        if (Directory.Exists(fullPath)) throw new IOException("The export destination is a directory.");
        if (!File.Exists(fullPath)) return new(fullPath, null, default, default);
        FileInfo file = new(fullPath);
        if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0)
            throw new IOException("The PNG destination is redirected or read-only.");
        return new(fullPath, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc);
    }

    public PreparedPngExport Prepare(
        PngExportDestination destination, PreviewPngPayload payload,
        string? documentPath, CancellationToken cancellationToken = default)
    {
        if (!new PreviewPngPayloadValidator().IsValid(payload.Bytes, payload.Size))
            throw new InvalidDataException("The rendered PNG failed validation.");
        // Match the existing Clipboard/Drag decoder safeguard as well as bridge framing checks.
        try
        {
            using MemoryStream png = new(payload.Bytes, writable: false);
            PngBitmapDecoder decoder = new(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count != 1 || decoder.Frames[0].PixelWidth != payload.Size.Width
                || decoder.Frames[0].PixelHeight != payload.Size.Height)
                throw new InvalidDataException("The decoded PNG dimensions do not match the request.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
            or ArgumentException or FormatException or NotSupportedException or COMException)
        {
            throw new InvalidDataException("The rendered PNG could not be decoded safely.", exception);
        }
        if (CaptureDestination(destination.Path, documentPath) != destination)
            throw new IOException("The PNG destination changed. Choose the destination again.");
        string temporaryPath = Path.Combine(Path.GetDirectoryName(destination.Path)!, $".SvgLiveEditor-{Guid.NewGuid():N}.png.tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16_384, FileOptions.WriteThrough))
            {
                stream.Write(payload.Bytes);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(this, destination, temporaryPath, documentPath, cancellationToken);
        }
        catch
        {
            // Cleanup must also run for cancellation; the original exception is preserved.
            DeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    internal static void DeleteTemporaryFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The destination is untouched; cleanup is best-effort if folder access was revoked.
        }
    }
}

public sealed class PreparedPngExport : IDisposable
{
    private readonly PngExportFileService _service;
    private readonly PngExportDestination _destination;
    private readonly string? _documentPath;
    private string? _temporaryPath;
    private CancellationTokenRegistration _cancellationRegistration;

    internal PreparedPngExport(PngExportFileService service, PngExportDestination destination,
        string temporaryPath, string? documentPath, CancellationToken cancellationToken)
    {
        _service = service;
        _destination = destination;
        _temporaryPath = temporaryPath;
        _documentPath = documentPath;
        _cancellationRegistration = cancellationToken.Register(Dispose);
    }

    public void Commit(Func<bool> isApplicable)
    {
        if (_temporaryPath is not string temporaryPath) throw new InvalidOperationException("The PNG write is no longer available.");
        if (_service.CaptureDestination(_destination.Path, _documentPath) != _destination)
            throw new IOException("The PNG destination changed. Choose the destination again.");
        // Called on the UI thread immediately before atomic finalization, after asynchronous staging.
        if (!isApplicable()) throw new OperationCanceledException("The document changed or PNG export was canceled.");
        if (_destination.Length is null) File.Move(temporaryPath, _destination.Path);
        else File.Replace(temporaryPath, _destination.Path, destinationBackupFileName: null);
        _temporaryPath = null;
        _cancellationRegistration.Dispose();
    }

    public void Dispose()
    {
        string? temporaryPath = Interlocked.Exchange(ref _temporaryPath, null);
        if (temporaryPath is not null) PngExportFileService.DeleteTemporaryFile(temporaryPath);
        _cancellationRegistration.Dispose();
    }
}
