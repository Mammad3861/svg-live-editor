namespace SvgLiveEditor.Services;

public sealed class SvgFontFamilyStackService
{
    private static readonly string[] DefaultFallbacks =
        ["Segoe UI", "Tahoma", "sans-serif"];

    private static readonly HashSet<string> GenericFamilies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "sans-serif",
            "serif",
            "monospace"
        };

    public bool TryCreateForSuggestion(
        string existingValue,
        string selectedFamily,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(existingValue);
        ArgumentNullException.ThrowIfNull(selectedFamily);
        value = string.Empty;
        string selectedName = Unquote(selectedFamily.Trim());
        if (selectedName.Length == 0
            || selectedName.Contains(',')
            || SvgFontFamilyValueValidator.Validate(selectedName) is not null)
        {
            return false;
        }

        string formattedSelection = FormatFamily(selectedName);
        if (GenericFamilies.Contains(selectedName))
        {
            value = formattedSelection;
            return true;
        }

        List<string> families = [formattedSelection];
        IReadOnlyList<string> fallbacks =
            TrySplit(existingValue, out IReadOnlyList<string>? existing)
            && existing.Count > 1
                ? existing.Skip(1).ToArray()
                : DefaultFallbacks
                    .Select(FormatFamily)
                    .ToArray();
        foreach (string fallback in fallbacks)
        {
            if (!Unquote(fallback).Equals(
                    selectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                families.Add(fallback);
            }
        }

        while (families.Count > 1)
        {
            string candidate = string.Join(", ", families);
            if (SvgFontFamilyValueValidator.Validate(candidate) is null)
            {
                value = candidate;
                return true;
            }
            families.RemoveAt(families.Count - 1);
        }

        value = families[0];
        return SvgFontFamilyValueValidator.Validate(value) is null;
    }

    private static bool TrySplit(
        string value,
        out IReadOnlyList<string> families)
    {
        families = [];
        if (string.IsNullOrWhiteSpace(value)
            || SvgFontFamilyValueValidator.Validate(value) is not null)
        {
            return false;
        }

        string[] parts = value
            .Split(',')
            .Select(part => part.Trim())
            .ToArray();
        if (parts.Length == 0 || parts.Any(part => part.Length == 0))
        {
            return false;
        }

        families = parts;
        return true;
    }

    private static string FormatFamily(string value)
    {
        string family = Unquote(value.Trim());
        return family.Any(char.IsWhiteSpace)
            ? $"\"{family}\""
            : family;
    }

    private static string Unquote(string value)
    {
        return value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
    }
}
