namespace SvgLiveEditor.Services;

public sealed class PreviewDragGestureTracker
{
    private double _startX;
    private double _startY;

    public bool IsArmed { get; private set; }

    public void Begin(double x, double y)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        _startX = x;
        _startY = y;
        IsArmed = true;
    }

    public bool Move(
        double x,
        double y,
        bool isLeftButtonPressed,
        double minimumHorizontalDistance,
        double minimumVerticalDistance)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        ValidateDistance(
            minimumHorizontalDistance,
            nameof(minimumHorizontalDistance));
        ValidateDistance(
            minimumVerticalDistance,
            nameof(minimumVerticalDistance));

        if (!IsArmed || !isLeftButtonPressed)
        {
            Cancel();
            return false;
        }

        if (Math.Abs(x - _startX) < minimumHorizontalDistance
            && Math.Abs(y - _startY) < minimumVerticalDistance)
        {
            return false;
        }

        IsArmed = false;
        return true;
    }

    public void Cancel() => IsArmed = false;

    private static void ValidateCoordinate(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

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
