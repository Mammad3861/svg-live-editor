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

    public static bool TryReadExportSize(string widthText, string heightText, out PreviewPngSize size, out string error)
    {
        size = default;
        error = "Enter whole pixel dimensions from 1 to 4096 on each side.";
        if (!int.TryParse(widthText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int width)
            || !int.TryParse(heightText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int height)
            || width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension) return false;
        if ((long)width * height > MaximumPixelCount)
        {
            error = "The output must contain no more than 8,000,000 pixels. Reduce width or height.";
            return false;
        }
        size = new(width, height);
        error = string.Empty;
        return true;
    }

    public static double LinkedDimension(int value, SvgCanvasSize canvasSize, bool editingWidth)
    {
        ValidateDimension(canvasSize.Width, nameof(canvasSize));
        ValidateDimension(canvasSize.Height, nameof(canvasSize));
        if (value is < 1 or > MaximumDimension) throw new ArgumentOutOfRangeException(nameof(value));
        double linked = editingWidth ? value * (canvasSize.Height / canvasSize.Width) : value * (canvasSize.Width / canvasSize.Height);
        return Math.Max(1, Math.Round(linked, MidpointRounding.AwayFromZero));
    }

    public static SvgCanvasSize FitExport(SvgCanvasSize canvasSize, PreviewPngSize output)
    {
        ValidateDimension(canvasSize.Width, nameof(canvasSize));
        ValidateDimension(canvasSize.Height, nameof(canvasSize));
        if (output.Width is < 1 or > MaximumDimension || output.Height is < 1 or > MaximumDimension
            || output.PixelCount > MaximumPixelCount) throw new ArgumentOutOfRangeException(nameof(output));
        double scale = Math.Min(output.Width / canvasSize.Width, output.Height / canvasSize.Height);
        return new(canvasSize.Width * scale, canvasSize.Height * scale);
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
