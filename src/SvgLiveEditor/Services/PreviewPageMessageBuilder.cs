using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPageMessageBuilder
{
    public const double MaximumRenderedDimension = 10_000_000;

    public string BuildZoomStateMessage(
        string bridgeToken,
        double renderedWidth,
        double renderedHeight,
        PreviewViewportPosition viewport)
    {
        ValidateBridgeToken(bridgeToken);
        ValidateDimension(renderedWidth, nameof(renderedWidth));
        ValidateDimension(renderedHeight, nameof(renderedHeight));
        ValidateNormalized(viewport.CenterX, nameof(viewport));
        ValidateNormalized(viewport.CenterY, nameof(viewport));

        return JsonSerializer.Serialize(new
        {
            type = "zoomState",
            token = bridgeToken,
            renderedWidth,
            renderedHeight,
            centerX = viewport.CenterX,
            centerY = viewport.CenterY
        });
    }

    private static void ValidateBridgeToken(string bridgeToken)
    {
        ArgumentNullException.ThrowIfNull(bridgeToken);
        if (bridgeToken.Length != 32
            || bridgeToken.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "The preview bridge token must contain exactly 32 hexadecimal characters.",
                nameof(bridgeToken));
        }
    }

    private static void ValidateDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value)
            || value <= 0
            || value > MaximumRenderedDimension)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNormalized(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
