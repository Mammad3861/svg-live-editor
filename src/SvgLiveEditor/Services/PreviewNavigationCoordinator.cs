using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewNavigationCoordinator
{
    private long _nextRevision;
    private PreviewRenderRequest? _active;
    private PreviewRenderRequest? _pending;
    private PreviewRenderRequest? _lastSuccessful;

    public bool HasPending => _pending is not null;

    public bool TryEnqueue(
        long sourceRevision,
        string svg,
        SvgCanvasSize canvasSize,
        SvgVisualDocument visualDocument,
        PreviewZoomState zoomState,
        PreviewViewportPosition viewport,
        bool force,
        out PreviewRenderRequest? request)
    {
        ArgumentNullException.ThrowIfNull(svg);
        ArgumentNullException.ThrowIfNull(visualDocument);
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }
        if (!force
            && (MatchesSource(_pending, sourceRevision, svg)
                || (_pending is null
                    && MatchesSource(_active, sourceRevision, svg))
                || (_active is null
                    && _pending is null
                    && MatchesSource(
                        _lastSuccessful,
                        sourceRevision,
                        svg))))
        {
            request = null;
            return false;
        }

        request = new PreviewRenderRequest(
            checked(++_nextRevision),
            sourceRevision,
            svg,
            canvasSize,
            visualDocument,
            zoomState,
            viewport);
        _pending = request;
        return true;
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

    public bool TryComplete(
        long revision,
        bool isSuccess,
        out bool wasLatest)
    {
        wasLatest = false;
        if (_active?.Revision != revision)
        {
            return false;
        }

        PreviewRenderRequest completed = _active;
        _active = null;
        wasLatest = revision == _nextRevision;
        if (isSuccess && wasLatest && _pending is null)
        {
            _lastSuccessful = completed;
        }
        return true;
    }

    public void Reset()
    {
        _active = null;
        _pending = null;
        _lastSuccessful = null;
    }

    private static bool MatchesSource(
        PreviewRenderRequest? request,
        long sourceRevision,
        string svg) =>
        request?.SourceRevision == sourceRevision
        && request.Svg.Equals(svg, StringComparison.Ordinal);
}
