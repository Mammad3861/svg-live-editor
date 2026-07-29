using System.Windows.Media;

namespace SvgLiveEditor.Services;

public sealed class InstalledFontFamilyProvider
{
    private static readonly string[] PinnedFamilies =
    [
        "Segoe UI",
        "Arial",
        "Tahoma",
        "sans-serif",
        "serif",
        "monospace"
    ];

    private readonly Lazy<IReadOnlyList<string>> _families;

    public InstalledFontFamilyProvider()
        : this(() => Fonts.SystemFontFamilies.Select(font => font.Source))
    {
    }

    public InstalledFontFamilyProvider(
        Func<IEnumerable<string>> familyLoader)
    {
        ArgumentNullException.ThrowIfNull(familyLoader);
        _families = new Lazy<IReadOnlyList<string>>(
            () => LoadFamilies(familyLoader),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<string> GetFontFamilies() => _families.Value;

    private static IReadOnlyList<string> LoadFamilies(
        Func<IEnumerable<string>> familyLoader)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> result = [];
        foreach (string pinned in PinnedFamilies)
        {
            if (seen.Add(pinned))
            {
                result.Add(pinned);
            }
        }

        string[] installed;
        try
        {
            installed = familyLoader().ToArray();
        }
        catch
        {
            return result;
        }

        foreach (string family in installed
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(value => value.Trim())
                     .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
        {
            if (family.Length <= SvgFontFamilyValueValidator.MaximumLength
                && seen.Add(family))
            {
                result.Add(family);
            }
        }

        return result;
    }
}
