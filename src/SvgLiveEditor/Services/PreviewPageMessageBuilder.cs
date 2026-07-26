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

    public string BuildPanStateMessage(
        string bridgeToken,
        bool enabled)
    {
        ValidateHexToken(bridgeToken, nameof(bridgeToken));
        return JsonSerializer.Serialize(new
        {
            type = "panState",
            token = bridgeToken,
            enabled
        });
    }

    public string BuildPngRequestMessage(
        string bridgeToken,
        string requestId,
        PreviewPngSize size)
    {
        ValidateHexToken(bridgeToken, nameof(bridgeToken));
        ValidateHexToken(requestId, nameof(requestId));
        if (size.Width <= 0
            || size.Width > PreviewPngSizeCalculator.MaximumDimension
            || size.Height <= 0
            || size.Height > PreviewPngSizeCalculator.MaximumDimension
            || size.PixelCount > PreviewPngSizeCalculator.MaximumPixelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        return JsonSerializer.Serialize(new
        {
            type = "copyPng",
            token = bridgeToken,
            requestId,
            width = size.Width,
            height = size.Height
        });
    }

    private static void ValidateBridgeToken(string bridgeToken) =>
        ValidateHexToken(bridgeToken, nameof(bridgeToken));

    private static void ValidateHexToken(
        string value,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "The value must contain exactly 32 hexadecimal characters.",
                parameterName);
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
