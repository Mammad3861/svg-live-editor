using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SvgLiveEditor.Services;

public enum FontGlyphCoverage
{
    Unknown,
    Complete,
    Incomplete
}

public sealed class InstalledFontGlyphCoverageService
{
    private readonly Func<string, IReadOnlySet<int>?> _glyphLoader;

    public InstalledFontGlyphCoverageService()
        : this(LoadGlyphs)
    {
    }

    public InstalledFontGlyphCoverageService(
        Func<string, IReadOnlySet<int>?> glyphLoader)
    {
        _glyphLoader = glyphLoader
            ?? throw new ArgumentNullException(nameof(glyphLoader));
    }

    public FontGlyphCoverage Check(
        string fontFamily,
        string text)
    {
        ArgumentNullException.ThrowIfNull(fontFamily);
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(fontFamily)
            || text.Length == 0)
        {
            return FontGlyphCoverage.Unknown;
        }

        IReadOnlySet<int>? glyphs;
        try
        {
            glyphs = _glyphLoader(fontFamily);
        }
        catch
        {
            return FontGlyphCoverage.Unknown;
        }
        if (glyphs is null)
        {
            return FontGlyphCoverage.Unknown;
        }

        bool checkedCharacter = false;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                continue;
            }

            checkedCharacter = true;
            if (!glyphs.Contains(rune.Value))
            {
                return FontGlyphCoverage.Incomplete;
            }
        }

        return checkedCharacter
            ? FontGlyphCoverage.Complete
            : FontGlyphCoverage.Unknown;
    }

    private static IReadOnlySet<int>? LoadGlyphs(string familyName)
    {
        FontFamily family = new(familyName);
        Typeface typeface = new(
            family,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out GlyphTypeface? glyphTypeface)
            ? glyphTypeface.CharacterToGlyphMap.Keys.ToHashSet()
            : null;
    }
}
