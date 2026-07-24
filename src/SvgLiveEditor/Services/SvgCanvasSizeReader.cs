using System.Globalization;
using System.IO;
using System.Xml;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgCanvasSizeReader
{
    private const double DefaultWidth = 300;
    private const double DefaultHeight = 150;

    public SvgCanvasSize Read(string validatedSvg)
    {
        ArgumentNullException.ThrowIfNull(validatedSvg);

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 5_000_000
        };

        using StringReader stringReader = new(validatedSvg);
        using XmlReader reader = XmlReader.Create(stringReader, settings);
        reader.MoveToContent();

        double? width = ParsePixelLength(reader.GetAttribute("width"));
        double? height = ParsePixelLength(reader.GetAttribute("height"));
        SvgCanvasSize? viewBox = ParseViewBox(reader.GetAttribute("viewBox"));

        if (width is not null && height is not null)
        {
            return new SvgCanvasSize(width.Value, height.Value);
        }

        if (viewBox is SvgCanvasSize viewBoxSize)
        {
            if (width is not null)
            {
                return new SvgCanvasSize(
                    width.Value,
                    width.Value * viewBoxSize.Height / viewBoxSize.Width);
            }

            if (height is not null)
            {
                return new SvgCanvasSize(
                    height.Value * viewBoxSize.Width / viewBoxSize.Height,
                    height.Value);
            }

            return viewBoxSize;
        }

        return new SvgCanvasSize(width ?? DefaultWidth, height ?? DefaultHeight);
    }

    private static double? ParsePixelLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^2].Trim();
        }
        else if (normalized.Any(char.IsLetter) || normalized.Contains('%', StringComparison.Ordinal))
        {
            return null;
        }

        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed)
            && double.IsFinite(parsed)
            && parsed > 0
                ? parsed
                : null;
    }

    private static SvgCanvasSize? ParseViewBox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] parts = value.Split(
            [' ', '\t', '\r', '\n', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
            || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
            || !double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0
            || height <= 0)
        {
            return null;
        }

        return new SvgCanvasSize(width, height);
    }
}
