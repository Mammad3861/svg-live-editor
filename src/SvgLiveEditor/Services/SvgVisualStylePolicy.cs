using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

internal static class SvgVisualStylePolicy
{
    public static bool HasAmbiguousSyntax(SvgElementNode element)
    {
        string? rawStyle = element.FindAttribute("style")?.RawValue;
        if (rawStyle is null)
        {
            return false;
        }
        if (!SvgXmlAttributeValueDecoder.TryDecode(
                rawStyle,
                out string style))
        {
            return true;
        }

        return style.Contains('\\')
            || style.Contains("/*", StringComparison.Ordinal)
            || style.Contains("*/", StringComparison.Ordinal)
            || style.Contains('!');
    }

    public static string? ReadPresentationValue(
        SvgElementNode element,
        string propertyName)
    {
        string? style = ReadDecodedAttribute(element, "style");
        string? styleValue = null;
        if (style is not null)
        {
            foreach (string declaration in style.Split(';'))
            {
                int separator = declaration.IndexOf(':');
                if (separator <= 0
                    || !declaration[..separator].Trim().Equals(
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                styleValue = declaration[(separator + 1)..].Trim();
            }
        }

        return styleValue ?? ReadDecodedAttribute(element, propertyName);
    }

    public static bool Defines(
        SvgElementNode element,
        string propertyName)
    {
        string? value = ReadStyleValue(element, propertyName);
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool IsDefinitelyNotRendered(
        SvgElementNode element,
        IReadOnlyDictionary<string, SvgElementNode> elementsByPath)
    {
        foreach (SvgElementNode current in EnumerateSelfAndAncestors(
                     element,
                     elementsByPath))
        {
            if (HasAmbiguousSyntax(current))
            {
                return false;
            }

            string? display = ReadPresentationValue(current, "display");
            if (display?.Trim().Equals(
                    "none",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            string? opacity = ReadPresentationValue(current, "opacity");
            if (IsZeroOpacity(opacity))
            {
                return true;
            }
        }

        foreach (SvgElementNode current in EnumerateSelfAndAncestors(
                     element,
                     elementsByPath))
        {
            string? visibility =
                ReadPresentationValue(current, "visibility")?.Trim();
            if (string.IsNullOrEmpty(visibility)
                || visibility.Equals(
                    "inherit",
                    StringComparison.OrdinalIgnoreCase)
                || visibility.Equals(
                    "unset",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return visibility.Equals(
                    "hidden",
                    StringComparison.OrdinalIgnoreCase)
                || visibility.Equals(
                    "collapse",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? ReadStyleValue(
        SvgElementNode element,
        string propertyName)
    {
        string? style = ReadDecodedAttribute(element, "style");
        string? value = null;
        if (style is null)
        {
            return null;
        }

        foreach (string declaration in style.Split(';'))
        {
            int separator = declaration.IndexOf(':');
            if (separator > 0
                && declaration[..separator].Trim().Equals(
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = declaration[(separator + 1)..].Trim();
            }
        }

        return value;
    }

    private static string? ReadDecodedAttribute(
        SvgElementNode element,
        string name)
    {
        string? rawValue = element.FindAttribute(name)?.RawValue;
        return rawValue is not null
            && SvgXmlAttributeValueDecoder.TryDecode(
                rawValue,
                out string decoded)
                ? decoded
                : null;
    }

    private static bool IsZeroOpacity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (normalized.EndsWith('%'))
        {
            normalized = normalized[..^1].Trim();
        }

        return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed)
            && double.IsFinite(parsed)
            && parsed == 0;
    }

    private static IEnumerable<SvgElementNode> EnumerateSelfAndAncestors(
        SvgElementNode element,
        IReadOnlyDictionary<string, SvgElementNode> elementsByPath)
    {
        SvgElementNode current = element;
        while (true)
        {
            yield return current;
            int separator = current.StructuralPath.LastIndexOf('/');
            if (separator < 0
                || !elementsByPath.TryGetValue(
                    current.StructuralPath[..separator],
                    out current!))
            {
                yield break;
            }
        }
    }
}
