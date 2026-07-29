using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed partial class RecoverySnapshotStore
{
    public const int MaximumSnapshotCount = 10;
    public const long MaximumTotalBytes = 100_000_000;
    public const long MaximumRevision = 1_000_000_000_000;
    public static readonly TimeSpan MaximumSnapshotAge = TimeSpan.FromDays(7);

    private const long MaximumSnapshotFileBytes = 70_000_000;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    private readonly object _gate = new();
    private readonly string _recoveryDirectory;
    private readonly Utf8FileService _fileService;
    private readonly SafeDocumentPathService _pathService;
    private readonly HashSet<string> _retiredSnapshotIds =
        new(StringComparer.Ordinal);

    public RecoverySnapshotStore()
        : this(
            new RecoveryDirectoryProvider().GetPath(),
            new Utf8FileService(),
            new SafeDocumentPathService())
    {
    }

    public RecoverySnapshotStore(
        string recoveryDirectory,
        Utf8FileService fileService,
        SafeDocumentPathService pathService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryDirectory);
        _recoveryDirectory = Path.GetFullPath(recoveryDirectory);
        _fileService = fileService
            ?? throw new ArgumentNullException(nameof(fileService));
        _pathService = pathService
            ?? throw new ArgumentNullException(nameof(pathService));
    }

    public string RecoveryDirectory => _recoveryDirectory;

    public static string CreateSnapshotId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();

    public static RecoverySnapshot CreateSnapshot(
        string snapshotId,
        string? originalPath,
        string displayName,
        string source,
        long revision,
        DateTimeOffset savedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(source);

        string? normalizedPath = string.IsNullOrWhiteSpace(originalPath)
            ? null
            : Path.GetFullPath(originalPath);
        return new RecoverySnapshot(
            RecoverySnapshot.CurrentSchemaVersion,
            snapshotId,
            normalizedPath,
            displayName,
            source,
            ComputeSourceHash(source),
            revision,
            savedUtc.ToUniversalTime(),
            normalizedPath is not null);
    }

    public PersistenceOperationResult TryWrite(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string? validationError = ValidateSnapshot(snapshot);
        if (validationError is not null)
        {
            return PersistenceOperationResult.Failure(validationError);
        }

        lock (_gate)
        {
            if (_retiredSnapshotIds.Contains(snapshot.SnapshotId))
            {
                return PersistenceOperationResult.Failure(
                    "The recovery snapshot belongs to a completed document session.");
            }

            try
            {
                EnsureRecoveryDirectory();
                string destinationPath = GetSnapshotPath(snapshot.SnapshotId);
                RecoverySnapshot? existing = TryReadSnapshot(destinationPath);
                if (existing is not null)
                {
                    if (ValidateSnapshot(existing) is null
                        && SnapshotFileNameMatches(
                            destinationPath,
                            existing)
                        && existing.Revision >= snapshot.Revision)
                    {
                        return PersistenceOperationResult.Success;
                    }

                    TryDeleteFile(destinationPath);
                }

                byte[] json = JsonSerializer.SerializeToUtf8Bytes(
                    snapshot,
                    JsonOptions);
                if (json.LongLength > MaximumSnapshotFileBytes)
                {
                    return PersistenceOperationResult.Failure(
                        "The recovery snapshot is too large.");
                }

                WriteAtomically(destinationPath, json);
                PruneCore(DateTimeOffset.UtcNow);
                if (!File.Exists(destinationPath))
                {
                    return PersistenceOperationResult.Failure(
                        "The recovery snapshot exceeded the bounded retention budget.");
                }

                return PersistenceOperationResult.Success;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or ArgumentException
                or NotSupportedException)
            {
                return PersistenceOperationResult.Failure(
                    $"Recovery could not save a local snapshot: {exception.Message}");
            }
        }
    }

    public IReadOnlyList<RecoveryCandidate> LoadMeaningfulCandidates(
        DateTimeOffset now)
    {
        lock (_gate)
        {
            try
            {
                EnsureRecoveryDirectory();
                PruneCore(now.ToUniversalTime());
                List<RecoveryCandidate> candidates = [];
                foreach (string path in EnumerateSnapshotFiles())
                {
                    RecoverySnapshot? snapshot = TryReadSnapshot(path);
                    if (snapshot is null)
                    {
                        TryDeleteFile(path);
                        continue;
                    }

                    string? validationError = ValidateSnapshot(snapshot);
                    if (validationError is not null
                        || !SnapshotFileNameMatches(path, snapshot))
                    {
                        TryDeleteFile(path);
                        continue;
                    }

                    string? restorablePath = GetRestorablePath(snapshot);
                    if (restorablePath is not null
                        && IsByteIdenticalToOriginal(snapshot, restorablePath))
                    {
                        TryDeleteFile(path);
                        continue;
                    }

                    candidates.Add(new RecoveryCandidate(
                        snapshot,
                        restorablePath));
                }

                return candidates
                    .OrderByDescending(candidate => candidate.Snapshot.SavedUtc)
                    .ThenByDescending(candidate => candidate.Snapshot.Revision)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                return [];
            }
        }
    }

    public bool TryDelete(string snapshotId, bool retire = true)
    {
        if (!IsValidSnapshotId(snapshotId))
        {
            return false;
        }

        lock (_gate)
        {
            if (retire)
            {
                _retiredSnapshotIds.Add(snapshotId);
            }

            try
            {
                if (!Directory.Exists(_recoveryDirectory))
                {
                    return true;
                }

                EnsureRecoveryDirectory();
                File.Delete(GetSnapshotPath(snapshotId));
                return true;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                return false;
            }
        }
    }

    public void Prune(DateTimeOffset now)
    {
        lock (_gate)
        {
            try
            {
                EnsureRecoveryDirectory();
                PruneCore(now.ToUniversalTime());
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                // Retention is best effort and must never interrupt editing.
            }
        }
    }

    public static string ComputeSourceHash(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Convert.ToHexString(
                SHA256.HashData(Utf8WithoutBom.GetBytes(source)))
            .ToLowerInvariant();
    }

    private string? GetRestorablePath(RecoverySnapshot snapshot)
    {
        if (!snapshot.IsNamed
            || snapshot.OriginalPath is not string originalPath)
        {
            return null;
        }

        SafeDocumentPathResult evaluation =
            _pathService.EvaluateExistingFile(
                originalPath,
                requireWritable: false);
        return evaluation.IsAllowed
            ? evaluation.FullPath
            : null;
    }

    private bool IsByteIdenticalToOriginal(
        RecoverySnapshot snapshot,
        string originalPath)
    {
        try
        {
            string original = _fileService.ReadAllText(originalPath);
            return original.Equals(snapshot.Source, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DecoderFallbackException)
        {
            return false;
        }
    }

    private string? ValidateSnapshot(RecoverySnapshot snapshot)
    {
        if (snapshot.SchemaVersion != RecoverySnapshot.CurrentSchemaVersion)
        {
            return "The recovery snapshot schema is unsupported.";
        }

        if (!IsValidSnapshotId(snapshot.SnapshotId))
        {
            return "The recovery snapshot identifier is invalid.";
        }

        if (string.IsNullOrWhiteSpace(snapshot.DisplayName)
            || snapshot.DisplayName.Length > 260
            || snapshot.DisplayName.Any(char.IsControl)
            || snapshot.DisplayName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return "The recovery snapshot display name is invalid.";
        }

        if (snapshot.Revision < 0
            || snapshot.Revision > MaximumRevision
            || snapshot.SavedUtc == default
            || snapshot.SavedUtc.Offset != TimeSpan.Zero
            || snapshot.SavedUtc > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return "The recovery snapshot metadata is invalid.";
        }

        if (snapshot.Source is null)
        {
            return "The recovery snapshot source is missing.";
        }

        int sourceBytes;
        try
        {
            sourceBytes = Utf8WithoutBom.GetByteCount(snapshot.Source);
        }
        catch (EncoderFallbackException)
        {
            return "The recovery snapshot source is not valid UTF-8 text.";
        }

        if (sourceBytes > Utf8FileService.MaximumFileBytes)
        {
            return "The recovery snapshot source exceeds the document size limit.";
        }

        if (string.IsNullOrWhiteSpace(snapshot.SourceSha256)
            || !HashPattern().IsMatch(snapshot.SourceSha256)
            || !snapshot.SourceSha256.Equals(
                ComputeSourceHash(snapshot.Source),
                StringComparison.Ordinal))
        {
            return "The recovery snapshot source hash is invalid.";
        }

        if (!snapshot.IsNamed)
        {
            return snapshot.OriginalPath is null
                ? null
                : "An untitled recovery snapshot cannot contain an original path.";
        }

        if (string.IsNullOrWhiteSpace(snapshot.OriginalPath)
            || !Path.IsPathFullyQualified(snapshot.OriginalPath)
            || !LastDocumentService.IsSupportedPath(snapshot.OriginalPath))
        {
            return "The recovery snapshot original path is invalid.";
        }

        try
        {
            if (!Path.GetFullPath(snapshot.OriginalPath).Equals(
                    snapshot.OriginalPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "The recovery snapshot original path is not normalized.";
            }
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return "The recovery snapshot original path is invalid.";
        }

        return null;
    }

    private static bool IsValidSnapshotId(string snapshotId) =>
        !string.IsNullOrWhiteSpace(snapshotId)
        && SnapshotIdPattern().IsMatch(snapshotId);

    private string GetSnapshotPath(string snapshotId)
    {
        if (!IsValidSnapshotId(snapshotId))
        {
            throw new ArgumentException(
                "The recovery snapshot identifier is invalid.",
                nameof(snapshotId));
        }

        string path = Path.GetFullPath(Path.Combine(
            _recoveryDirectory,
            $"recovery-{snapshotId}.json"));
        string prefix = _recoveryDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                "The recovery snapshot path escaped the recovery directory.");
        }

        return path;
    }

    private void EnsureRecoveryDirectory()
    {
        string? parent = Path.GetDirectoryName(_recoveryDirectory);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
            if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "The recovery parent directory is redirected.");
            }
        }

        Directory.CreateDirectory(_recoveryDirectory);
        FileAttributes attributes = File.GetAttributes(_recoveryDirectory);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
            != FileAttributes.Directory)
        {
            throw new IOException(
                "The recovery directory is redirected or is not a directory.");
        }

    }

    private IEnumerable<string> EnumerateSnapshotFiles() =>
        Directory.EnumerateFiles(
            _recoveryDirectory,
            "recovery-*.json",
            SearchOption.TopDirectoryOnly);

    private RecoverySnapshot? TryReadSnapshot(string path)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists
                || file.Length <= 0
                || file.Length > MaximumSnapshotFileBytes
                || (file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16_384,
                FileOptions.SequentialScan);
            return JsonSerializer.Deserialize<RecoverySnapshot>(
                stream,
                JsonOptions);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    private void WriteAtomically(
        string destinationPath,
        byte[] bytes)
    {
        string temporaryPath = Path.Combine(
            _recoveryDirectory,
            $".recovery-{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(
                temporaryPath,
                destinationPath,
                overwrite: true);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private void PruneCore(DateTimeOffset now)
    {
        DateTime cutoffUtc = (now - MaximumSnapshotAge).UtcDateTime;
        foreach (string temporaryPath in Directory.EnumerateFiles(
            _recoveryDirectory,
            ".recovery-*.tmp",
            SearchOption.TopDirectoryOnly))
        {
            TryDeleteFile(temporaryPath);
        }

        List<FileInfo> boundedFiles = [];
        long boundedBytes = 0;
        foreach (FileInfo file in EnumerateSnapshotFiles()
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal))
        {
            if (!file.Exists
                || file.Length <= 0
                || file.Length > MaximumSnapshotFileBytes
                || (file.Attributes & FileAttributes.ReparsePoint) != 0
                || !RecoveryRetentionPolicy.CanKeep(
                    boundedFiles.Count,
                    boundedBytes,
                    file.Length))
            {
                TryDeleteFile(file.FullName);
                continue;
            }

            boundedFiles.Add(file);
            boundedBytes += file.Length;
        }

        List<SnapshotFile> files = [];
        foreach (FileInfo file in boundedFiles)
        {
            RecoverySnapshot? snapshot = TryReadSnapshot(file.FullName);
            if (snapshot is null
                || ValidateSnapshot(snapshot) is not null
                || !SnapshotFileNameMatches(file.FullName, snapshot)
                || snapshot.SavedUtc.UtcDateTime < cutoffUtc)
            {
                TryDeleteFile(file.FullName);
                continue;
            }

            files.Add(new SnapshotFile(file, snapshot));
        }

        files = files
            .OrderByDescending(file => file.Snapshot.SavedUtc)
            .ThenByDescending(file => file.Snapshot.Revision)
            .ThenByDescending(
                file => file.File.Name,
                StringComparer.Ordinal)
            .ToList();

        long totalBytes = files.Sum(file => file.File.Length);
        for (int index = files.Count - 1;
             index >= 0
             && (files.Count > MaximumSnapshotCount
                 || totalBytes > MaximumTotalBytes);
             index--)
        {
            SnapshotFile oldest = files[index];
            TryDeleteFile(oldest.File.FullName);
            totalBytes -= oldest.File.Length;
            files.RemoveAt(index);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // Cleanup failures are non-fatal and retried during later pruning.
        }
    }

    private static bool SnapshotFileNameMatches(
        string path,
        RecoverySnapshot snapshot)
    {
        string expected = $"recovery-{snapshot.SnapshotId}.json";
        return Path.GetFileName(path).Equals(
            expected,
            StringComparison.Ordinal);
    }

    [GeneratedRegex("^[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex SnapshotIdPattern();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    private sealed record SnapshotFile(
        FileInfo File,
        RecoverySnapshot Snapshot);
}
