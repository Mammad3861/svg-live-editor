using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InstalledFontFamilyProviderTests
{
    [TestMethod]
    public void EnumerationIsCachedPinnedSortedAndDeduplicated()
    {
        int calls = 0;
        InstalledFontFamilyProvider provider = new(() =>
        {
            calls++;
            return ["Zulu Font", "Arial", "alpha Font", "Tahoma"];
        });

        IReadOnlyList<string> first = provider.GetFontFamilies();
        IReadOnlyList<string> second = provider.GetFontFamilies();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, calls);
        CollectionAssert.AreEqual(
            new[]
            {
                "Segoe UI", "Arial", "Tahoma",
                "sans-serif", "serif", "monospace",
                "alpha Font", "Zulu Font"
            },
            first.ToArray());
    }

    [TestMethod]
    public void LoaderFailureStillReturnsPinnedLocalAndGenericFamilies()
    {
        InstalledFontFamilyProvider provider = new(() =>
            throw new InvalidOperationException("font registry unavailable"));

        CollectionAssert.AreEqual(
            new[]
            {
                "Segoe UI", "Arial", "Tahoma",
                "sans-serif", "serif", "monospace"
            },
            provider.GetFontFamilies().ToArray());
    }
}
