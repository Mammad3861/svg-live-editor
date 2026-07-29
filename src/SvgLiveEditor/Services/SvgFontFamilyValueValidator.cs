namespace SvgLiveEditor.Services;

public static class SvgFontFamilyValueValidator
{
    public const int MaximumLength = 256;

    public static string? Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return null;
        }
        if (value.Length > MaximumLength)
        {
            return $"Font family must be {MaximumLength} characters or fewer.";
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Font family cannot contain only whitespace.";
        }
        if (value.Any(char.IsControl))
        {
            return "Font family cannot contain control characters.";
        }
        if (value.Contains("url(", StringComparison.OrdinalIgnoreCase)
            || value.IndexOfAny([';', '{', '}', '<', '>', '\\', '@']) >= 0)
        {
            return "Font family contains unsupported CSS or markup characters.";
        }

        foreach (string part in value.Split(','))
        {
            string family = part.Trim();
            if (family.Length == 0)
            {
                return "Each font family in the fallback stack must have a name.";
            }

            bool quoted = family.Length >= 2
                && ((family[0] == '"' && family[^1] == '"')
                    || (family[0] == '\'' && family[^1] == '\''));
            if (family.Contains('"') || family.Contains('\''))
            {
                if (!quoted
                    || family[1..^1].Contains('"')
                    || family[1..^1].Contains('\''))
                {
                    return "Font-family quotes must wrap one complete family name.";
                }
                family = family[1..^1];
            }

            if (family.Length == 0
                || family.Any(character =>
                    !(char.IsLetterOrDigit(character)
                      || char.IsWhiteSpace(character)
                      || character is '-' or '_' or '.')))
            {
                return "Font family contains unsupported characters.";
            }
        }

        return null;
    }
}
