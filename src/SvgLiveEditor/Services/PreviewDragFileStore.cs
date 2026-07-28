using System.IO;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewDragFileStore
{
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    public const int MaximumFileCount = 20;
    public const long MaximumTotalBytes = 200_000_000;

    private const string ManagedFilePrefix = "SvgLiveEditor-";
    private const string ManagedFileExtension = ".png";

    private readonly PreviewPngPayloadValidator _payloadValidator;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _maximumAge;
    private readonly int _maximumFileCount;
    private readonly long _maximumTotalBytes;

    public PreviewDragFileStore(
        string? directoryPath = null,
        Func<DateTimeOffset>? utcNow = null,
        PreviewPngPayloadValidator? payloadValidator = null,
        TimeSpan? maximumAge = null,
        int maximumFileCount = MaximumFileCount,
        long maximumTotalBytes = MaximumTotalBytes)
    {
        string configuredPath = directoryPath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SvgLiveEditor",
            "DragOut");
        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new ArgumentException(
                "The drag-out directory must be fully qualified.",
                nameof(directoryPath));
        }

        DirectoryPath = Path.GetFullPath(configuredPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _payloadValidator =
            payloadValidator ?? new PreviewPngPayloadValidator();
        _maximumAge = maximumAge ?? MaximumAge;
        if (_maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        }

        if (maximumFileCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFileCount));
        }

        if (maximumTotalBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTotalBytes));
        }

        _maximumFileCount = maximumFileCount;
        _maximumTotalBytes = maximumTotalBytes;
    }

    public string DirectoryPath { get; }

    public PreviewDragFileResult TryCreate(
        PreviewPngPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!_payloadValidator.IsValid(payload.Bytes, payload.Size))
        {
            return PreviewDragFileResult.Failure(
                "The rendered PNG failed host validation.");
        }

        try
        {
            TryCleanup();
            Directory.CreateDirectory(DirectoryPath);

            for (int attempt = 0; attempt < 4; attempt++)
            {
                string fileName =
                    $"{ManagedFilePrefix}{Guid.NewGuid():N}{ManagedFileExtension}";
                string path = Path.Combine(DirectoryPath, fileName);
                if (!IsManagedPath(path))
                {
                    return PreviewDragFileResult.Failure(
                        "The temporary PNG path failed validation.");
                }

                try
                {
                    using FileStream stream = new(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.Read,
                        bufferSize: 16_384,
                        FileOptions.WriteThrough);
                    stream.Write(payload.Bytes);
                    stream.Flush(flushToDisk: true);
                    TryCleanup();
                    return PreviewDragFileResult.Success(path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // A cryptographically random collision is unlikely, but
                    // CreateNew keeps the no-overwrite guarantee explicit.
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return PreviewDragFileResult.Failure(exception.Message);
        }

        return PreviewDragFileResult.Failure(
            "A unique temporary PNG name could not be created.");
    }

    public bool TryDelete(string path)
    {
        if (!IsManagedPath(path))
        {
            return false;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryCleanup()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return true;
            }

            bool succeeded = true;
            DateTimeOffset cutoff = _utcNow() - _maximumAge;
            List<FileInfo> files = Directory
                .EnumerateFiles(
                    DirectoryPath,
                    $"{ManagedFilePrefix}*{ManagedFileExtension}",
                    SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            foreach (FileInfo file in files.ToArray())
            {
                if (file.LastWriteTimeUtc < cutoff.UtcDateTime)
                {
                    succeeded &= TryDelete(file.FullName);
                    files.Remove(file);
                }
            }

            long retainedBytes = 0;
            int retainedCount = 0;
            foreach (FileInfo file in files)
            {
                bool exceedsBound =
                    retainedCount >= _maximumFileCount
                    || file.Length >
                        _maximumTotalBytes - retainedBytes;
                if (exceedsBound)
                {
                    succeeded &= TryDelete(file.FullName);
                    continue;
                }

                retainedCount++;
                retainedBytes += file.Length;
            }

            return succeeded;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }
    }

    private bool IsManagedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            string? parent = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);
            return string.Equals(
                    parent,
                    DirectoryPath,
                    StringComparison.OrdinalIgnoreCase)
                && fileName.StartsWith(
                    ManagedFilePrefix,
                    StringComparison.Ordinal)
                && fileName.EndsWith(
                    ManagedFileExtension,
                    StringComparison.OrdinalIgnoreCase)
                && fileName.Length ==
                    ManagedFilePrefix.Length + 32
                    + ManagedFileExtension.Length
                && fileName
                    .AsSpan(
                        ManagedFilePrefix.Length,
                        32)
                    .ToString()
                    .All(Uri.IsHexDigit);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }
}
