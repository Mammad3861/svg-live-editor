using System.Globalization;
using System.Text;

namespace SvgLiveEditor.Services;

public sealed class SvgTextDirectionAdvisoryService
{
    public const string RtlTextWithLtrDirection =
        "Text begins with RTL content but direction is LTR. Set direction to RTL for RTL anchoring.";

    public const string LtrTextWithRtlDirection =
        "Text begins with LTR content but direction is RTL. Set direction to LTR for LTR anchoring.";

    public string? GetWarning(string text, string direction)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(direction);
        StrongDirection firstStrong = FindFirstStrongDirection(text);
        return (firstStrong, direction) switch
        {
            (StrongDirection.RightToLeft, "ltr") =>
                RtlTextWithLtrDirection,
            (StrongDirection.LeftToRight, "rtl") =>
                LtrTextWithRtlDirection,
            _ => null
        };
    }

    private static StrongDirection FindFirstStrongDirection(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is not (
                    UnicodeCategory.UppercaseLetter
                    or UnicodeCategory.LowercaseLetter
                    or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter
                    or UnicodeCategory.OtherLetter))
            {
                continue;
            }

            return IsRightToLeftScript(rune.Value)
                ? StrongDirection.RightToLeft
                : StrongDirection.LeftToRight;
        }

        return StrongDirection.None;
    }

    private static bool IsRightToLeftScript(int value) =>
        value is >= 0x0590 and <= 0x08FF
        or >= 0xFB1D and <= 0xFDFF
        or >= 0xFE70 and <= 0xFEFF;

    private enum StrongDirection
    {
        None,
        LeftToRight,
        RightToLeft
    }
}
