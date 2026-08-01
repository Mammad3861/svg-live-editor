using System.Globalization;

namespace SvgLiveEditor.Services;

public static class SvgVisualLengthParser
{
    public const double MaximumAbsoluteValue = 1_000_000_000;

    public static bool TryParse(
        string? rawValue,
        double defaultValue,
        out double value,
        out string suffix)
    {
        suffix = string.Empty;
        if (rawValue is null)
        {
            value = defaultValue;
            return true;
        }

        string normalized = rawValue.Trim();
        if (normalized.EndsWith(
                "px",
                StringComparison.OrdinalIgnoreCase))
        {
            suffix = normalized[^2..];
            normalized = normalized[..^2].TrimEnd();
        }

        if (normalized.Length == 0
            || normalized.Any(character =>
                char.IsLetter(character) || character == '%')
            || !double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            || !double.IsFinite(value)
            || Math.Abs(value) > MaximumAbsoluteValue)
        {
            value = 0;
            suffix = string.Empty;
            return false;
        }

        return true;
    }
}
