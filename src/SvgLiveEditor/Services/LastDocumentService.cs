using System.IO;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class LastDocumentService
{
    private static readonly string[] SupportedExtensions = [".svg", ".txt"];

    private readonly Utf8FileService _fileService;

    public LastDocumentService()
        : this(new Utf8FileService())
    {
    }

    public LastDocumentService(Utf8FileService fileService)
    {
        _fileService = fileService
            ?? throw new ArgumentNullException(nameof(fileService));
    }

    public LastDocumentRestoreResult TryRestore(UserPreferences preferences)
    {
        if (!preferences.ReopenLastDocumentOnStartup
            || string.IsNullOrWhiteSpace(preferences.LastDocumentPath))
        {
            return LastDocumentRestoreResult.NotRequested;
        }

        if (!TryNormalizeSupportedPath(
                preferences.LastDocumentPath,
                out string path)
            || !File.Exists(path))
        {
            return LastDocumentRestoreResult.Unavailable;
        }

        try
        {
            return LastDocumentRestoreResult.Restored(
                _fileService.ReadAllText(path),
                path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DecoderFallbackException)
        {
            return LastDocumentRestoreResult.Unavailable;
        }
    }

    public UserPreferences Remember(
        UserPreferences preferences,
        string path)
    {
        if (!TryNormalizeSupportedPath(path, out string normalizedPath))
        {
            throw new ArgumentException(
                "The last document must be a full SVG or TXT path.",
                nameof(path));
        }

        return preferences with { LastDocumentPath = normalizedPath };
    }

    public UserPreferences Forget(UserPreferences preferences) =>
        preferences with { LastDocumentPath = null };

    public static bool IsSupportedPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && SupportedExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeSupportedPath(
        string path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        try
        {
            if (!Path.IsPathFullyQualified(path) || !IsSupportedPath(path))
            {
                return false;
            }

            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }
}
