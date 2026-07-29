using System.IO;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SafeDocumentPathService
{
    private static readonly string[] SupportedExtensions = [".svg", ".txt"];

    public SafeDocumentPathResult EvaluateExistingFile(
        string? path,
        bool requireWritable)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SafeDocumentPathResult.Blocked(
                "Auto Save requires a named SVG or TXT document.");
        }

        string fullPath;
        try
        {
            if (!Path.IsPathFullyQualified(path))
            {
                return SafeDocumentPathResult.Blocked(
                    "The document path is not fully qualified.");
            }

            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return SafeDocumentPathResult.Blocked(
                "The document path is not supported.");
        }

        if (!SupportedExtensions.Contains(
                Path.GetExtension(fullPath),
                StringComparer.OrdinalIgnoreCase))
        {
            return SafeDocumentPathResult.Blocked(
                "Auto Save supports only SVG and TXT documents.");
        }

        if (fullPath.StartsWith(
                @"\\",
                StringComparison.Ordinal))
        {
            return SafeDocumentPathResult.Blocked(
                "Auto Save is paused for network paths.");
        }

        try
        {
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return SafeDocumentPathResult.Blocked(
                    "The document drive is not supported.");
            }

            DriveType driveType = new DriveInfo(root).DriveType;
            if (driveType is not (DriveType.Fixed or DriveType.Removable))
            {
                return SafeDocumentPathResult.Blocked(
                    "Auto Save is paused for this drive type.");
            }

            if (!File.Exists(fullPath))
            {
                return SafeDocumentPathResult.Blocked(
                    "Auto Save is paused because the original file is missing.");
            }

            FileAttributes fileAttributes = File.GetAttributes(fullPath);
            if ((fileAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                return SafeDocumentPathResult.Blocked(
                    "Auto Save is paused for redirected or non-file targets.");
            }

            if (requireWritable
                && (fileAttributes & FileAttributes.ReadOnly) != 0)
            {
                return SafeDocumentPathResult.Blocked(
                    "Auto Save is paused because the original file is read-only.");
            }

            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)
                || ContainsReparsePoint(directory, root))
            {
                return SafeDocumentPathResult.Blocked(
                    "Auto Save is paused for redirected folders.");
            }

            return SafeDocumentPathResult.Allowed(fullPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return SafeDocumentPathResult.Blocked(
                "The document path is inaccessible under the Auto Save policy.");
        }
    }

    private static bool ContainsReparsePoint(
        string directory,
        string root)
    {
        string current = Path.GetFullPath(directory);
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        while (current.Length > normalizedRoot.Length)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent)
                || parent.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return false;
    }
}
