using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class WindowsClipboardWriter : IClipboardWriter
{
    private const string PngClipboardFormat = "PNG";

    public void WriteText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        DataObject data = new();
        data.SetData(DataFormats.UnicodeText, text);
        data.SetData(DataFormats.Text, text);
        Clipboard.SetDataObject(data, copy: true);
    }

    public void WritePng(PreviewPngPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        BitmapImage bitmap = CreateBitmap(payload.Bytes);
        MemoryStream pngStream = new(payload.Bytes, writable: false);

        DataObject data = new();
        // Bitmap is broadly compatible with Windows paste targets, while the PNG
        // format preserves alpha for applications that understand it.
        data.SetData(DataFormats.Bitmap, bitmap);
        data.SetData(PngClipboardFormat, pngStream);
        Clipboard.SetDataObject(data, copy: true);
    }

    private static BitmapImage CreateBitmap(byte[] pngBytes)
    {
        using MemoryStream stream = new(pngBytes, writable: false);
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
