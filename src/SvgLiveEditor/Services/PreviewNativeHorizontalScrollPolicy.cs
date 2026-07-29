using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewNativeHorizontalScrollPolicy
{
    public const int WheelDelta = 120;
    public const uint PageScroll = uint.MaxValue;
    public const double CssPixelsPerScrollCharacter = 16;
    public const uint MaximumScrollCharacters = 100;
    public const double MaximumDeltaPixels = 10_000;

    public bool TryCreateRequest(
        NativeHorizontalWheelInput input,
        PreviewNativeScrollContext context,
        uint scrollCharacters,
        out PreviewNativeScrollRequest request)
    {
        request = default;
        if (context.State != PreviewNativeInputState.Ready
            || context.IsNavigating
            || !context.IsPointerOverPreview
            || input.Delta == 0
            || input.ControlHeld
            || !IsValidToken(context.NavigationToken)
            || !double.IsFinite(context.ViewportWidth)
            || context.ViewportWidth <= 0
            || scrollCharacters == 0)
        {
            return false;
        }

        double unitPixels = scrollCharacters == PageScroll
            ? context.ViewportWidth
            : Math.Min(scrollCharacters, MaximumScrollCharacters)
                * CssPixelsPerScrollCharacter;
        double rawDelta = input.Delta / (double)WheelDelta * unitPixels;
        if (!double.IsFinite(rawDelta) || rawDelta == 0)
        {
            return false;
        }

        double maximum = Math.Min(
            MaximumDeltaPixels,
            context.ViewportWidth * 4);
        double boundedDelta = Math.Clamp(rawDelta, -maximum, maximum);
        request = new PreviewNativeScrollRequest(
            context.NavigationToken!,
            boundedDelta);
        return true;
    }

    private static bool IsValidToken(string? token)
    {
        return token is not null
            && token.Length == 32
            && token.All(Uri.IsHexDigit);
    }
}
