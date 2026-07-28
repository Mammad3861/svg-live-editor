using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class InboundFileDropPolicy
{
    private static readonly string[] SupportedExtensions = [".svg", ".txt"];
    private readonly Func<string, DriveType> _driveTypeResolver;
    private readonly Func<string, FileAttributes> _attributeReader;

    public InboundFileDropPolicy()
        : this(
            rootPath => new DriveInfo(rootPath).DriveType,
            File.GetAttributes)
    {
    }

    public InboundFileDropPolicy(
        Func<string, DriveType> driveTypeResolver,
        Func<string, FileAttributes> attributeReader)
    {
        _driveTypeResolver = driveTypeResolver
            ?? throw new ArgumentNullException(nameof(driveTypeResolver));
        _attributeReader = attributeReader
            ?? throw new ArgumentNullException(nameof(attributeReader));
    }

    public InboundFileDropEvaluation Evaluate(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            if (!data.GetDataPresent(
                    DataFormats.FileDrop,
                    autoConvert: false)
                || data.GetData(
                    DataFormats.FileDrop,
                    autoConvert: false) is not string[] files)
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.EmptyPayload,
                    "Only one local SVG or TXT file can be dropped.");
            }

            return Evaluate(files);
        }
        catch (Exception exception) when (exception is ExternalException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.UnreadablePayload,
                "The dropped data could not be read safely.");
        }
    }

    public InboundFileDropEvaluation Evaluate(
        IReadOnlyList<string>? files)
    {
        if (files is null || files.Count == 0)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.EmptyPayload,
                "The drop did not contain a file.");
        }

        if (files.Count != 1)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.MultipleFiles,
                "Drop one SVG or TXT file at a time.");
        }

        string candidate = files[0];
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Contains("://", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(candidate)
            || candidate.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.NotLocalFile,
                "Only one local SVG or TXT file can be dropped.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.NotLocalFile,
                "The dropped path is not a valid local file.");
        }

        string? rootPath = Path.GetPathRoot(fullPath);
        try
        {
            if (string.IsNullOrWhiteSpace(rootPath)
                || !IsLocalDriveType(_driveTypeResolver(rootPath)))
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.NotLocalFile,
                    "Network and redirected drives cannot be opened by dropping.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.NotLocalFile,
                "The dropped path is not on an available local drive.");
        }

        string extension = Path.GetExtension(fullPath);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.Shortcut,
                "Windows shortcuts cannot be opened by dropping.");
        }

        if (!SupportedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.UnsupportedExtension,
                "SvgLiveEditor accepts only .svg and .txt files.");
        }

        try
        {
            if (HasReparsePointAncestor(fullPath, rootPath))
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.ReparsePoint,
                    "Files reached through linked or redirected folders cannot be opened by dropping.");
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.UnreadableFile,
                "The dropped file path cannot be inspected safely.");
        }

        if (Directory.Exists(fullPath))
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.Directory,
                "Folders cannot be opened by dropping.");
        }

        if (!File.Exists(fullPath))
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.MissingFile,
                "The dropped file is no longer available.");
        }

        try
        {
            FileInfo file = new(fullPath);
            if ((file.Attributes & FileAttributes.Directory) != 0)
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.Directory,
                    "Folders cannot be opened by dropping.");
            }

            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.ReparsePoint,
                    "Linked or redirected files cannot be opened by dropping.");
            }

            if (file.Length > Utf8FileService.MaximumFileBytes)
            {
                return InboundFileDropEvaluation.Rejected(
                    InboundFileDropRejection.OversizedFile,
                    $"The file exceeds the {Utf8FileService.MaximumFileMegabytes} MB limit.");
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return InboundFileDropEvaluation.Rejected(
                InboundFileDropRejection.UnreadableFile,
                "The dropped file cannot be read.");
        }

        return InboundFileDropEvaluation.Accepted(
            fullPath,
            SanitizeDisplayFileName(Path.GetFileName(fullPath)));
    }

    private static string SanitizeDisplayFileName(string fileName)
    {
        string sanitized = new(
            fileName
                .Where(character => !char.IsControl(character))
                .Take(120)
                .ToArray());
        return string.IsNullOrWhiteSpace(sanitized)
            ? "SVG or TXT"
            : sanitized;
    }

    private static bool IsLocalDriveType(DriveType driveType)
    {
        return driveType is DriveType.Fixed
            or DriveType.Removable
            or DriveType.CDRom
            or DriveType.Ram;
    }

    private bool HasReparsePointAncestor(
        string fullPath,
        string rootPath)
    {
        string relativePath = fullPath[rootPath.Length..];
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        string currentPath = rootPath;

        for (int index = 0; index < segments.Length - 1; index++)
        {
            currentPath = Path.Combine(currentPath, segments[index]);
            if ((_attributeReader(currentPath)
                    & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
