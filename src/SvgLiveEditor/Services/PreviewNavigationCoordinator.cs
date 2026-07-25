using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewNavigationCoordinator
{
    private long _nextRevision;
    private PreviewRenderRequest? _active;
    private PreviewRenderRequest? _pending;

    public bool HasPending => _pending is not null;

    public PreviewRenderRequest Enqueue(
        string svg,
        SvgCanvasSize canvasSize,
        PreviewZoomState zoomState,
        PreviewViewportPosition viewport)
    {
        ArgumentNullException.ThrowIfNull(svg);
        PreviewRenderRequest request = new(
            checked(++_nextRevision),
            svg,
            canvasSize,
            zoomState,
            viewport);
        _pending = request;
        return request;
    }

    public PreviewRenderRequest? TryBeginNext()
    {
        if (_active is not null || _pending is null)
        {
            return null;
        }

        _active = _pending;
        _pending = null;
        return _active;
    }

    public bool TryComplete(long revision, out bool wasLatest)
    {
        wasLatest = false;
        if (_active?.Revision != revision)
        {
            return false;
        }

        _active = null;
        wasLatest = revision == _nextRevision;
        return true;
    }

    public void Reset()
    {
        _active = null;
        _pending = null;
    }
}
