using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class ClipboardCopyService
{
    private readonly IClipboardWriter _writer;
    private readonly ClipboardRetryService _retryService;

    public ClipboardCopyService(
        IClipboardWriter writer,
        ClipboardRetryService? retryService = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _retryService = retryService ?? new ClipboardRetryService();
    }

    public Task<ClipboardWriteResult> CopyTextAsync(
        string exactText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exactText);
        return _retryService.TryWriteAsync(
            () => _writer.WriteText(exactText),
            cancellationToken);
    }

    public Task<ClipboardWriteResult> CopyPngAsync(
        PreviewPngPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return _retryService.TryWriteAsync(
            () => _writer.WritePng(payload),
            cancellationToken);
    }
}
