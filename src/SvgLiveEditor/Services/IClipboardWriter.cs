using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public interface IClipboardWriter
{
    void WriteText(string text);

    void WritePng(PreviewPngPayload payload);
}
