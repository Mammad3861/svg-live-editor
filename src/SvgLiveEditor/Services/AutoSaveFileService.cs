using System.IO;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class AutoSaveFileService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly SafeDocumentPathService _pathService;

    public AutoSaveFileService()
        : this(new SafeDocumentPathService())
    {
    }

    public AutoSaveFileService(SafeDocumentPathService pathService)
    {
        _pathService = pathService
            ?? throw new ArgumentNullException(nameof(pathService));
    }

    public AutoSavePrepareResult Prepare(
        string path,
        string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        int byteCount;
        try
        {
            byteCount = Utf8WithoutBom.GetByteCount(source);
        }
        catch (EncoderFallbackException)
        {
            return AutoSavePrepareResult.Failure(
                "Auto Save is paused because the source cannot be encoded as valid UTF-8.");
        }
        if (byteCount > Utf8FileService.MaximumFileBytes)
        {
            return AutoSavePrepareResult.Failure(
                $"Auto Save is paused because the document exceeds {Utf8FileService.MaximumFileMegabytes} MB.");
        }

        SafeDocumentPathResult evaluation =
            _pathService.EvaluateExistingFile(path, requireWritable: true);
        if (!evaluation.IsAllowed || evaluation.FullPath is not string fullPath)
        {
            return AutoSavePrepareResult.Failure(evaluation.StatusMessage);
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return AutoSavePrepareResult.Failure(
                "Auto Save could not resolve the document folder.");
        }

        string temporaryPath = Path.Combine(
            directory,
            $".SvgLiveEditor-{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = Utf8WithoutBom.GetBytes(source);
            using FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                FileOptions.WriteThrough);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
            return AutoSavePrepareResult.Prepared(
                new PreparedAutoSave(
                    fullPath,
                    temporaryPath,
                    _pathService));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            return AutoSavePrepareResult.Failure(
                $"Auto Save failed while staging the document: {exception.Message}");
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // Best-effort cleanup only; the user's original remains untouched.
        }
    }
}

public sealed class PreparedAutoSave : IDisposable
{
    private readonly SafeDocumentPathService _pathService;
    private string? _temporaryPath;

    internal PreparedAutoSave(
        string destinationPath,
        string temporaryPath,
        SafeDocumentPathService pathService)
    {
        DestinationPath = destinationPath;
        _temporaryPath = temporaryPath;
        _pathService = pathService;
    }

    public string DestinationPath { get; }

    public PersistenceOperationResult Commit()
    {
        if (_temporaryPath is not string temporaryPath)
        {
            return PersistenceOperationResult.Failure(
                "The staged Auto Save is no longer available.");
        }

        SafeDocumentPathResult evaluation =
            _pathService.EvaluateExistingFile(
                DestinationPath,
                requireWritable: true);
        if (!evaluation.IsAllowed
            || evaluation.FullPath is not string fullPath
            || !fullPath.Equals(
                DestinationPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return PersistenceOperationResult.Failure(
                evaluation.StatusMessage);
        }

        try
        {
            File.Replace(
                temporaryPath,
                DestinationPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
            _temporaryPath = null;
            return PersistenceOperationResult.Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return PersistenceOperationResult.Failure(
                $"Auto Save could not replace the original file atomically: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_temporaryPath is not string temporaryPath)
        {
            return;
        }

        _temporaryPath = null;
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // Best-effort cleanup only; the user's original is never touched here.
        }
    }
}

public readonly record struct AutoSavePrepareResult(
    bool Succeeded,
    PreparedAutoSave? PreparedWrite,
    string? ErrorMessage)
{
    public static AutoSavePrepareResult Prepared(
        PreparedAutoSave preparedWrite) =>
        new(true, preparedWrite, null);

    public static AutoSavePrepareResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
