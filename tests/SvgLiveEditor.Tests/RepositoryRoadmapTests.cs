namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class RepositoryRoadmapTests
{
    [TestMethod]
    public void RoadmapsExistAndUseTheSameVersionMilestones()
    {
        string english = ReadDocument("roadmap.md");
        string persian = ReadDocument("roadmap.fa.md");

        Assert.IsFalse(string.IsNullOrWhiteSpace(english));
        Assert.IsFalse(string.IsNullOrWhiteSpace(persian));
        foreach (string version in new[] { "v0.7.1", "v0.8", "v0.9", "v1.0" })
        {
            StringAssert.Contains(english, version);
            StringAssert.Contains(persian, version);
        }
    }

    [TestMethod]
    public void EnglishRoadmapContainsTheFinalAgreedProductDecision()
    {
        string roadmap = ReadDocument("roadmap.md");
        string[] requiredPhrases =
        [
            "v0.1 through v0.7",
            "Source editor context menu",
            "Compact Layer feedback",
            "Visible Property help",
            "Properties Undo/Redo routing",
            "Real Layers/Groups architecture and UI",
            "same-parent layer limitation without unsafe implicit reparenting",
            "Insert basic SVG elements",
            "Multi-selection",
            "Alignment",
            "Snapping",
            "Reliability and data-loss review",
            "Persistence and recovery validation",
            "Advanced selection visual redesign",
            "Visual color editing",
            "Expanded Templates",
            "Keyboard customization",
            "Appearance and Theme system",
            "System (default), Light, and Dark",
            "Never modify SVG artwork",
            "Advanced vector editing",
            "Future integrations",
            "not promises or deadlines"
        ];

        foreach (string phrase in requiredPhrases)
        {
            StringAssert.Contains(roadmap, phrase);
        }
    }

    [TestMethod]
    public void PersianRoadmapContainsEquivalentFinalScope()
    {
        string roadmap = ReadDocument("roadmap.fa.md");
        string[] requiredPhrases =
        [
            "نسخه‌های v0.1 تا v0.7",
            "منوی زمینهٔ ویرایشگر Source",
            "بازخورد فشردهٔ Layer",
            "راهنمای دیداری Property",
            "مسیریابی Undo/Redo در Properties",
            "معماری و رابط واقعی Layers/Groups",
            "بدون جابه‌جایی ضمنی و ناامن بین والدها",
            "درج عناصر پایهٔ SVG",
            "انتخاب چندگانه",
            "هم‌ترازی",
            "چسبیدن به راهنماها و نقاط",
            "خطر از دست رفتن داده",
            "اعتبارسنجی ماندگاری و بازیابی",
            "بازطراحی پیشرفتهٔ ظاهر انتخاب",
            "ویرایش دیداری رنگ",
            "گسترش Templates",
            "سفارشی‌سازی صفحه‌کلید",
            "سامانهٔ ظاهر و Theme",
            "System (پیش‌فرض)، Light و Dark",
            "هرگز نباید خود اثر SVG را تغییر دهد",
            "ویرایش پیشرفتهٔ برداری",
            "یکپارچه‌سازی‌های آینده",
            "نه وعده یا ضرب‌الاجل"
        ];

        foreach (string phrase in requiredPhrases)
        {
            StringAssert.Contains(roadmap, phrase);
        }
    }

    [TestMethod]
    public void ReadmesLinkToTheMatchingLanguageRoadmap()
    {
        string englishReadme = ReadRepositoryDocument("README.md");
        string persianReadme = ReadRepositoryDocument("README.fa.md");

        StringAssert.Contains(englishReadme, "[official roadmap](docs/roadmap.md)");
        StringAssert.Contains(
            persianReadme,
            "[نقشهٔ راه رسمی](docs/roadmap.fa.md)");
    }

    private static string ReadDocument(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "docs", fileName));

    private static string ReadRepositoryDocument(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "repository",
            fileName));
}
