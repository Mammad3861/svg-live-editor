using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPngSizeCalculator
{
    public const int MaximumDimension = 4096;
    public const long MaximumPixelCount = 8_000_000;
    public const double MaximumReasonableSvgDimension = 1_000_000_000;

    public PreviewPngSize Calculate(SvgCanvasSize canvasSize)
    {
        ValidateDimension(canvasSize.Width, nameof(canvasSize));
        ValidateDimension(canvasSize.Height, nameof(canvasSize));

        double scale = Math.Min(
            1,
            Math.Min(
                MaximumDimension / canvasSize.Width,
                MaximumDimension / canvasSize.Height));
        double scaledPixelCount =
            canvasSize.Width * scale * canvasSize.Height * scale;
        if (!double.IsFinite(scaledPixelCount) || scaledPixelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvasSize));
        }

        if (scaledPixelCount > MaximumPixelCount)
        {
            scale *= Math.Sqrt(MaximumPixelCount / scaledPixelCount);
        }

        int width = Math.Clamp(
            (int)Math.Floor(canvasSize.Width * scale),
            1,
            MaximumDimension);
        int height = Math.Clamp(
            (int)Math.Floor(canvasSize.Height * scale),
            1,
            MaximumDimension);

        while ((long)width * height > MaximumPixelCount)
        {
            if (width >= height)
            {
                width--;
            }
            else
            {
                height--;
            }
        }

        return new PreviewPngSize(width, height);
    }

    private static void ValidateDimension(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value)
            || value <= 0
            || value > MaximumReasonableSvgDimension)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
