using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace SvgLiveEditor.Services;

public sealed partial class SvgPropertyValueValidator
{
    private const int MaximumPropertyLength = 100_000;

    private static readonly HashSet<string> CoordinateAttributes =
        new(StringComparer.Ordinal)
        {
            "x",
            "y",
            "cx",
            "cy",
            "x1",
            "y1",
            "x2",
            "y2"
        };

    private static readonly HashSet<string> NonNegativeLengthAttributes =
        new(StringComparer.Ordinal)
        {
            "width",
            "height",
            "r",
            "rx",
            "ry",
            "stroke-width"
        };

    public string? Validate(string elementName, string attributeName, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentNullException.ThrowIfNull(value);

        SvgLiveEditor.Models.SvgPropertyDefinition? definition =
            SvgPropertySchema.Find(elementName, attributeName);
        if (definition is null)
        {
            return $"Attribute '{attributeName}' is not editable for {elementName}.";
        }

        if (definition.IsReadOnly)
        {
            return $"Attribute '{attributeName}' is read-only in this version.";
        }

        if (value.Length > MaximumPropertyLength)
        {
            return "The value is too long.";
        }

        if (value.Any(character =>
                char.IsControl(character)
                && character is not '\t' and not '\r' and not '\n'))
        {
            return "The value contains unsupported control characters.";
        }

        return attributeName switch
        {
            "id" => ValidateId(value),
            "fill" or "stroke" => ValidatePaint(value),
            "opacity" => ValidateOpacity(value),
            "viewBox" => ValidateViewBox(value),
            _ when CoordinateAttributes.Contains(attributeName) =>
                ValidateLength(value, requireNonNegative: false),
            _ when NonNegativeLengthAttributes.Contains(attributeName) =>
                ValidateLength(value, requireNonNegative: true),
            _ => null
        };
    }

    private static string? ValidateId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ID cannot be empty.";
        }

        try
        {
            XmlConvert.VerifyNCName(value);
            return null;
        }
        catch (XmlException)
        {
            return "ID must be a valid XML name without spaces.";
        }
    }

    private static string? ValidatePaint(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return "Paint value cannot be empty.";
        }

        if (PaintKeywordPattern().IsMatch(normalized)
            || HexColorPattern().IsMatch(normalized)
            || ColorFunctionPattern().IsMatch(normalized)
            || FragmentPaintPattern().IsMatch(normalized))
        {
            return null;
        }

        return "Use a color, none, or an internal url(#id) paint.";
    }

    private static string? ValidateOpacity(string value)
    {
        string normalized = value.Trim();
        if (normalized.EndsWith('%'))
        {
            return double.TryParse(
                    normalized[..^1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double percentage)
                && double.IsFinite(percentage)
                && percentage is >= 0 and <= 100
                    ? null
                    : "Opacity percentage must be between 0% and 100%.";
        }

        return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double opacity)
            && double.IsFinite(opacity)
            && opacity is >= 0 and <= 1
                ? null
                : "Opacity must be between 0 and 1.";
    }

    private static string? ValidateViewBox(string value)
    {
        string[] parts = value.Split(
            [' ', '\t', '\r', '\n', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return "viewBox requires four numbers.";
        }

        double[] numbers = new double[4];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!double.TryParse(
                    parts[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out numbers[index])
                || !double.IsFinite(numbers[index]))
            {
                return "viewBox requires four finite numbers.";
            }
        }

        return numbers[2] > 0 && numbers[3] > 0
            ? null
            : "viewBox width and height must be greater than zero.";
    }

    private static string? ValidateLength(string value, bool requireNonNegative)
    {
        Match match = LengthPattern().Match(value.Trim());
        if (!match.Success
            || !double.TryParse(
                match.Groups["number"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number)
            || !double.IsFinite(number))
        {
            return "Enter a finite SVG length such as 12, -4.5, 20px, or 50%.";
        }

        return requireNonNegative && number < 0
            ? "This length cannot be negative."
            : null;
    }

    [GeneratedRegex(
        "^(?:none|currentColor|transparent|inherit|initial|unset|context-fill|context-stroke|[A-Za-z]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PaintKeywordPattern();

    [GeneratedRegex(
        "^#[0-9A-Fa-f]{3,8}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();

    [GeneratedRegex(
        "^(?:rgb|rgba|hsl|hsla)\\(\\s*[-+0-9.,%/\\s]+\\)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ColorFunctionPattern();

    [GeneratedRegex(
        "^url\\(\\s*#[^\\s()]+\\s*\\)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FragmentPaintPattern();

    [GeneratedRegex(
        "^(?<number>[+-]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[eE][+-]?\\d+)?)(?:px|%|em|rem|cm|mm|in|pt|pc)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LengthPattern();
}
