using System.Globalization;
using System.Text;

namespace SvgLiveEditor.Services;

internal static class SvgXmlAttributeValueDecoder
{
    public static bool TryDecode(string value, out string decoded)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Contains('&'))
        {
            decoded = value;
            return true;
        }

        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current != '&')
            {
                builder.Append(current);
                continue;
            }

            int terminator = value.IndexOf(';', index + 1);
            if (terminator < 0
                || !TryDecodeEntity(
                    value[(index + 1)..terminator],
                    out string? replacement))
            {
                decoded = string.Empty;
                return false;
            }

            builder.Append(replacement);
            index = terminator;
        }

        decoded = builder.ToString();
        return true;
    }

    private static bool TryDecodeEntity(
        string entity,
        out string? replacement)
    {
        replacement = entity switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            _ => null
        };
        if (replacement is not null)
        {
            return true;
        }

        ReadOnlySpan<char> digits;
        NumberStyles styles;
        if (entity.StartsWith("#x", StringComparison.Ordinal)
            || entity.StartsWith("#X", StringComparison.Ordinal))
        {
            digits = entity.AsSpan(2);
            styles = NumberStyles.AllowHexSpecifier;
        }
        else if (entity.StartsWith('#'))
        {
            digits = entity.AsSpan(1);
            styles = NumberStyles.None;
        }
        else
        {
            return false;
        }

        if (digits.IsEmpty
            || !int.TryParse(
                digits,
                styles,
                CultureInfo.InvariantCulture,
                out int codePoint)
            || !IsXmlCharacter(codePoint))
        {
            return false;
        }

        replacement = char.ConvertFromUtf32(codePoint);
        return true;
    }

    private static bool IsXmlCharacter(int value) =>
        value is 0x9 or 0xA or 0xD
        || value is >= 0x20 and <= 0xD7FF
        || value is >= 0xE000 and <= 0xFFFD
        || value is >= 0x10000 and <= 0x10FFFF;
}
