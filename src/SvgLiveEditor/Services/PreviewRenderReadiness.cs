using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public enum PreviewRenderReadinessResult
{
    Ignored,
    Waiting,
    Ready,
    Error
}

public sealed class PreviewRenderReadiness
{
    private long? _renderRevision;
    private long? _sourceRevision;
    private bool _navigationCompleted;
    private bool _isTerminal;
    private PreviewImageLoadState? _imageState;

    public void Begin(long renderRevision, long sourceRevision)
    {
        if (renderRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderRevision));
        }
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }

        _renderRevision = renderRevision;
        _sourceRevision = sourceRevision;
        _navigationCompleted = false;
        _isTerminal = false;
        _imageState = null;
    }

    public PreviewRenderReadinessResult RecordNavigation(
        long renderRevision,
        bool isSuccess)
    {
        if (!IsCurrent(renderRevision) || _isTerminal)
        {
            return PreviewRenderReadinessResult.Ignored;
        }
        if (!isSuccess)
        {
            _isTerminal = true;
            return PreviewRenderReadinessResult.Error;
        }

        _navigationCompleted = true;
        return Resolve();
    }

    public PreviewRenderReadinessResult RecordImage(
        long renderRevision,
        PreviewImageLoadMessage message)
    {
        if (!IsCurrent(renderRevision)
            || _sourceRevision != message.SourceRevision
            || _isTerminal)
        {
            return PreviewRenderReadinessResult.Ignored;
        }

        _imageState = message.State;
        return Resolve();
    }

    public PreviewRenderReadinessResult Timeout(long renderRevision)
    {
        if (!IsCurrent(renderRevision) || _isTerminal)
        {
            return PreviewRenderReadinessResult.Ignored;
        }

        _isTerminal = true;
        return PreviewRenderReadinessResult.Error;
    }

    public void Reset()
    {
        _renderRevision = null;
        _sourceRevision = null;
        _navigationCompleted = false;
        _isTerminal = false;
        _imageState = null;
    }

    private PreviewRenderReadinessResult Resolve()
    {
        if (!_navigationCompleted || _imageState is null)
        {
            return PreviewRenderReadinessResult.Waiting;
        }

        _isTerminal = true;
        return _imageState == PreviewImageLoadState.Loaded
            ? PreviewRenderReadinessResult.Ready
            : PreviewRenderReadinessResult.Error;
    }

    private bool IsCurrent(long renderRevision) =>
        _renderRevision == renderRevision;
}
