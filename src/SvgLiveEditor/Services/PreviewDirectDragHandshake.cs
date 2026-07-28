using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewDirectDragHandshake
{
    private readonly PreviewPointerGestureArbiter _arbiter;
    private PreviewDirectDragArmRequest? _armedRequest;

    public PreviewDirectDragHandshake(
        PreviewPointerGestureArbiter? arbiter = null)
    {
        _arbiter = arbiter ?? new PreviewPointerGestureArbiter();
    }

    public bool IsArmed => _armedRequest is not null;

    public bool TryArm(
        PreviewDirectDragArmRequest request,
        bool isLeftButtonPressed)
    {
        if (!isLeftButtonPressed
            || _arbiter.Resolve(request.Gesture)
                != PreviewPointerGestureAction.OutboundDrag)
        {
            Reset();
            return false;
        }

        _armedRequest = request;
        return true;
    }

    public bool TryStart(
        PreviewDirectDragSignal signal,
        bool isLeftButtonPressed,
        bool isPanModeEnabled,
        double minimumHorizontalDistance,
        double minimumVerticalDistance)
    {
        ValidateDistance(
            minimumHorizontalDistance,
            nameof(minimumHorizontalDistance));
        ValidateDistance(
            minimumVerticalDistance,
            nameof(minimumVerticalDistance));

        if (_armedRequest is not PreviewDirectDragArmRequest armed
            || signal.Action != PreviewDirectDragSignalAction.Start
            || !string.Equals(
                signal.GestureId,
                armed.GestureId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!isLeftButtonPressed
            || isPanModeEnabled
            || signal.ViewportWidth != armed.ViewportWidth
            || signal.ViewportHeight != armed.ViewportHeight)
        {
            Reset();
            return false;
        }

        if (Math.Abs(signal.X - armed.X) < minimumHorizontalDistance
            && Math.Abs(signal.Y - armed.Y) < minimumVerticalDistance)
        {
            return false;
        }

        Reset();
        return true;
    }

    public bool TryCancel(PreviewDirectDragSignal signal)
    {
        if (_armedRequest is not PreviewDirectDragArmRequest armed
            || signal.Action != PreviewDirectDragSignalAction.Cancel
            || !string.Equals(
                signal.GestureId,
                armed.GestureId,
                StringComparison.Ordinal))
        {
            return false;
        }

        Reset();
        return true;
    }

    public void Reset() => _armedRequest = null;

    private static void ValidateDistance(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
