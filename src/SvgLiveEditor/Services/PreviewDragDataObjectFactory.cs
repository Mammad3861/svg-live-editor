using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewDragDataObjectFactory
{
    public const string PngDataFormat = "PNG";

    private readonly PreviewPngPayloadValidator _payloadValidator;

    public PreviewDragDataObjectFactory(
        PreviewPngPayloadValidator? payloadValidator = null)
    {
        _payloadValidator =
            payloadValidator ?? new PreviewPngPayloadValidator();
    }

    public DataObject Create(
        PreviewPngPayload payload,
        string temporaryPngPath)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPngPath);
        if (!_payloadValidator.IsValid(payload.Bytes, payload.Size))
        {
            throw new ArgumentException(
                "The PNG payload failed validation.",
                nameof(payload));
        }

        string fullPath = Path.GetFullPath(temporaryPngPath);
        if (!Path.IsPathFullyQualified(temporaryPngPath)
            || !Path.GetExtension(fullPath)
                .Equals(".png", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            throw new ArgumentException(
                "The drag-out PNG must be an existing full path.",
                nameof(temporaryPngPath));
        }

        BitmapImage bitmap;
        try
        {
            bitmap = CreateBitmap(payload.Bytes);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or NotSupportedException
            or COMException)
        {
            throw new ArgumentException(
                "The PNG payload could not be decoded safely.",
                nameof(payload),
                exception);
        }

        MemoryStream pngStream =
            new(payload.Bytes, writable: false);
        StringCollection files = [fullPath];

        DataObject data = new();
        data.SetFileDropList(files);
        data.SetData(DataFormats.Bitmap, bitmap);
        data.SetData(PngDataFormat, pngStream);
        return data;
    }

    private static BitmapImage CreateBitmap(byte[] pngBytes)
    {
        using MemoryStream stream =
            new(pngBytes, writable: false);
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
