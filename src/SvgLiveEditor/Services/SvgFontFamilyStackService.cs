using System.Text.RegularExpressions;

namespace SvgLiveEditor.Services;

public sealed record SvgFontFamilyStack(
    string PrimaryFamily,
    IReadOnlyList<string> SerializedFallbacks,
    string SerializedValue);

public sealed class SvgFontFamilyStackService
{
    private static readonly string[] DefaultFallbacks =
        ["Segoe UI", "Tahoma", "sans-serif"];

    private static readonly HashSet<string> TerminalFamilies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "sans-serif",
            "serif",
            "monospace",
            "inherit",
            "initial",
            "unset"
        };

    private static readonly char[] ForbiddenCharacters =
        [';', '{', '}', '<', '>', '\\', '@'];

    public bool TryParse(
        string value,
        out SvgFontFamilyStack stack)
    {
        ArgumentNullException.ThrowIfNull(value);
        stack = new SvgFontFamilyStack(string.Empty, [], string.Empty);
        if (!TryParseEntries(
                value,
                out IReadOnlyList<FontFamilyEntry> entries,
                out _)
            || entries.Count == 0)
        {
            return false;
        }

        stack = new SvgFontFamilyStack(
            entries[0].Name,
            entries.Skip(1)
                .Select(entry => entry.Serialized)
                .ToArray(),
            string.Join(", ", entries.Select(entry => entry.Serialized)));
        return true;
    }

    public bool TryCreateForPrimary(
        string existingValue,
        string primaryValue,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(existingValue);
        ArgumentNullException.ThrowIfNull(primaryValue);
        value = string.Empty;
        if (primaryValue.Length == 0)
        {
            return true;
        }
        if (!TryReadPrimaryName(primaryValue, out string primaryFamily)
            || !TryFormatFamily(primaryFamily, out string serializedPrimary))
        {
            return false;
        }
        if (TerminalFamilies.Contains(primaryFamily))
        {
            value = serializedPrimary;
            return true;
        }

        IReadOnlyList<string> fallbackEntries =
            TryParse(existingValue, out SvgFontFamilyStack existing)
            && existing.SerializedFallbacks.Count > 0
                ? existing.SerializedFallbacks
                : DefaultFallbacks
                    .Select(fallback =>
                    {
                        _ = TryFormatFamily(
                            fallback,
                            out string serializedFallback);
                        return serializedFallback;
                    })
                    .ToArray();
        List<string> serializedFamilies = [serializedPrimary];
        HashSet<string> names = new(
            [primaryFamily],
            StringComparer.OrdinalIgnoreCase);
        foreach (string fallback in fallbackEntries)
        {
            if (!TryParseEntries(
                    fallback,
                    out IReadOnlyList<FontFamilyEntry> parsedFallback,
                    out _)
                || parsedFallback.Count != 1)
            {
                return false;
            }
            if (names.Add(parsedFallback[0].Name))
            {
                serializedFamilies.Add(parsedFallback[0].Serialized);
            }
        }

        string candidate = string.Join(", ", serializedFamilies);
        while (candidate.Length > SvgFontFamilyValueValidator.MaximumLength
               && serializedFamilies.Count > 1)
        {
            serializedFamilies.RemoveAt(serializedFamilies.Count - 1);
            candidate = string.Join(", ", serializedFamilies);
        }
        if (ValidateSerializedValue(candidate) is not null)
        {
            return false;
        }

        value = candidate;
        return true;
    }

    public bool TryCreateForSuggestion(
        string existingValue,
        string selectedFamily,
        out string value) =>
        TryCreateForPrimary(existingValue, selectedFamily, out value);

    public static string? ValidateSerializedValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return null;
        }

        return TryParseEntries(value, out _, out string? error)
            ? null
            : error;
    }

    private static bool TryReadPrimaryName(
        string value,
        out string family)
    {
        family = value.Trim();
        if (family.Length == 0
            || family.Length > SvgFontFamilyValueValidator.MaximumLength)
        {
            return false;
        }

        bool startsWithQuote = family[0] is '\'' or '"';
        bool endsWithQuote = family[^1] is '\'' or '"';
        if (startsWithQuote || endsWithQuote)
        {
            if (!TryParseEntries(
                    family,
                    out IReadOnlyList<FontFamilyEntry> entries,
                    out _)
                || entries.Count != 1)
            {
                return false;
            }

            family = entries[0].Name;
        }
        return IsSafeFamilyName(family);
    }

    private static bool TryParseEntries(
        string value,
        out IReadOnlyList<FontFamilyEntry> entries,
        out string? error)
    {
        entries = [];
        error = null;
        if (value.Length > SvgFontFamilyValueValidator.MaximumLength)
        {
            error =
                $"Font family must be {SvgFontFamilyValueValidator.MaximumLength} characters or fewer.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Font family cannot contain only whitespace.";
            return false;
        }
        if (value.Any(char.IsControl))
        {
            error = "Font family cannot contain control characters.";
            return false;
        }
        if (Regex.IsMatch(
                value,
                "url\\s*\\(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || value.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            error =
                "Font family contains unsupported CSS or markup characters.";
            return false;
        }

        List<FontFamilyEntry> parsed = [];
        int offset = 0;
        while (offset < value.Length)
        {
            SkipWhitespace(value, ref offset);
            if (offset >= value.Length)
            {
                error =
                    "Each font family in the fallback stack must have a name.";
                return false;
            }

            int serializedStart = offset;
            string name;
            bool quoted = value[offset] is '\'' or '"';
            if (quoted)
            {
                char quote = value[offset++];
                int nameStart = offset;
                while (offset < value.Length && value[offset] != quote)
                {
                    offset++;
                }
                if (offset >= value.Length)
                {
                    error =
                        "Font-family quotes must wrap one complete family name.";
                    return false;
                }

                name = value[nameStart..offset];
                offset++;
                int serializedEnd = offset;
                SkipWhitespace(value, ref offset);
                if (offset < value.Length && value[offset] != ',')
                {
                    error =
                        "Quoted font families cannot contain trailing CSS syntax.";
                    return false;
                }
                if (!IsSafeFamilyName(name)
                    || !name.Equals(name.Trim(), StringComparison.Ordinal))
                {
                    error = "Font family contains unsupported characters.";
                    return false;
                }

                parsed.Add(new FontFamilyEntry(
                    name,
                    value[serializedStart..serializedEnd]));
            }
            else
            {
                while (offset < value.Length && value[offset] != ',')
                {
                    if (value[offset] is '\'' or '"')
                    {
                        error =
                            "Font-family quotes must wrap one complete family name.";
                        return false;
                    }
                    offset++;
                }

                string serialized = value[serializedStart..offset].Trim();
                name = serialized;
                if (!IsSafeFamilyName(name)
                    || RequiresQuotesForValidCss(name))
                {
                    error = "Font family contains unsupported characters.";
                    return false;
                }
                parsed.Add(new FontFamilyEntry(name, serialized));
            }

            if (offset >= value.Length)
            {
                break;
            }

            offset++;
            int next = offset;
            SkipWhitespace(value, ref next);
            if (next >= value.Length)
            {
                error =
                    "Each font family in the fallback stack must have a name.";
                return false;
            }
            offset = next;
        }

        entries = parsed;
        return parsed.Count > 0;
    }

    private static bool TryFormatFamily(
        string value,
        out string serialized)
    {
        serialized = string.Empty;
        string family = value.Trim();
        if (!IsSafeFamilyName(family))
        {
            return false;
        }
        if (!RequiresQuotes(family))
        {
            serialized = family;
            return serialized.Length
                <= SvgFontFamilyValueValidator.MaximumLength;
        }
        if (!family.Contains('"'))
        {
            serialized = $"\"{family}\"";
            return serialized.Length
                <= SvgFontFamilyValueValidator.MaximumLength;
        }
        if (!family.Contains('\''))
        {
            serialized = $"'{family}'";
            return serialized.Length
                <= SvgFontFamilyValueValidator.MaximumLength;
        }

        return false;
    }

    private static bool IsSafeFamilyName(string family)
    {
        return family.Length > 0
            && !string.IsNullOrWhiteSpace(family)
            && !family.Any(char.IsControl)
            && family.IndexOfAny(ForbiddenCharacters) < 0
            && !Regex.IsMatch(
                family,
                "url\\s*\\(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && family.All(character =>
                char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character is '-' or '_' or '.' or ','
                    or '\'' or '"' or '&' or '+' or '(' or ')');
    }

    private static bool RequiresQuotes(string family)
    {
        return char.IsDigit(family[0])
            || family.Any(character =>
                char.IsWhiteSpace(character)
                || character is ',' or '\'' or '"' or '&' or '+'
                    or '(' or ')');
    }

    private static bool RequiresQuotesForValidCss(string family)
    {
        return char.IsDigit(family[0])
            || family.Any(character =>
                character is ',' or '\'' or '"' or '&' or '+'
                    or '(' or ')');
    }

    private static void SkipWhitespace(string value, ref int offset)
    {
        while (offset < value.Length && char.IsWhiteSpace(value[offset]))
        {
            offset++;
        }
    }

    private sealed record FontFamilyEntry(
        string Name,
        string Serialized);
}
