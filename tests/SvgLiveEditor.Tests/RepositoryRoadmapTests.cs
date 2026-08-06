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
        foreach (string version in new[]
                 {
                     "v0.7.1",
                     "v0.8",
                     "v0.9.0",
                     "v0.10.0",
                     "v1.0.0"
                 })
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
            "v0.9.0 — Visual Authoring (current standalone release)",
            "Insert bounded basic SVG elements and empty groups",
            "safe deterministic ID/reference remapping",
            "Explicit conservative move into, out of, and between existing groups",
            "v0.10.0 — Visual Composition (planned)",
            "Multi-selection",
            "Moving multiple selected elements",
            "Group and Ungroup commands",
            "Alignment and distribution",
            "Basic snapping",
            "safe basic path bounding-box resize",
            "v1.0.0 — Stable Release / stabilization",
            "Reliability and data-loss review",
            "Persistence and recovery validation",
            "Advanced selection visual redesign",
            "Visual color editing",
            "Expanded Templates",
            "Keyboard customization",
            "Appearance and Theme system",
            "System (default), Light, and Dark",
            "Never modify SVG artwork",
            "Rulers, Guides & Smart Placement",
            "Horizontal and vertical rulers",
            "optional smart snapping that can be disabled",
            "Advanced vector editing",
            "Advanced exports and distribution",
            "Future integrations",
            "not promises or deadlines"
        ];

        foreach (string phrase in requiredPhrases)
        {
            StringAssert.Contains(roadmap, phrase);
        }
        Assert.IsFalse(roadmap.Contains("Stage 2", StringComparison.Ordinal));
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
            "v0.9.0 — Visual Authoring (انتشار مستقل جاری)",
            "درج محدود و امن عناصر پایهٔ SVG و گروه خالی",
            "بازنویسی امن و قطعی ID/reference",
            "انتقال صریح و محافظه‌کارانه به داخل، خارج و میان گروه‌های موجود",
            "v0.10.0 — Visual Composition (برنامه‌ریزی‌شده)",
            "انتخاب چندگانه",
            "جابه‌جایی چند عنصر انتخاب‌شده",
            "فرمان‌های Group و Ungroup",
            "هم‌ترازی و توزیع",
            "snapping پایه",
            "bounding-box برای path",
            "v1.0.0 — انتشار پایدار / پایدارسازی",
            "خطر از دست رفتن داده",
            "اعتبارسنجی ماندگاری و بازیابی",
            "بازطراحی پیشرفتهٔ ظاهر انتخاب",
            "ویرایش دیداری رنگ",
            "گسترش Templates",
            "سفارشی‌سازی صفحه‌کلید",
            "سامانهٔ ظاهر و Theme",
            "System (پیش‌فرض)، Light و Dark",
            "هرگز نباید خود اثر SVG را تغییر دهد",
            "خط‌کش‌ها، راهنماها و جای‌گذاری هوشمند",
            "خط‌کش افقی و عمودی",
            "snapping هوشمند اختیاری",
            "ویرایش پیشرفتهٔ برداری",
            "خروجی و توزیع پیشرفته",
            "یکپارچه‌سازی‌های آینده",
            "نه وعده یا ضرب‌الاجل"
        ];

        foreach (string phrase in requiredPhrases)
        {
            StringAssert.Contains(roadmap, phrase);
        }
        Assert.IsFalse(roadmap.Contains("مرحلهٔ ۲", StringComparison.Ordinal));
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

    [TestMethod]
    public void EnglishAndPersianDocsDescribeTheSameVisualAuthoringBoundaries()
    {
        string english = ReadRepositoryDocument("README.md");
        string persian = ReadRepositoryDocument("README.fa.md");
        string security = ReadDocument("security-model.md");

        foreach (string phrase in new[]
                 {
                     "Version 0.9.0 Visual Authoring is a complete standalone release",
                     "before, after, and inside-group feedback",
                     "session-only",
                     "Multi-selection, moving multiple selected elements, Group/Ungroup",
                     "v0.10.0 Visual Composition",
                     "Unsafe or ambiguous operations fail closed"
                 })
        {
            StringAssert.Contains(english, phrase);
        }
        foreach (string phrase in new[]
                 {
                     "نسخهٔ ۰٫۹٫۰ Visual Authoring یک انتشار مستقل و کامل",
                     "بازخورد جداگانهٔ before، after و داخل گروه",
                     "قفل فقط در نشست جاری",
                     "انتخاب چندگانه، حرکت چند عنصر انتخاب‌شده، Group/Ungroup",
                     "v0.10.0 Visual Composition",
                     "عملیات ناامن یا مبهم fail closed است"
                 })
        {
            StringAssert.Contains(persian, phrase);
        }
        StringAssert.Contains(security, "Layers and groups boundary");
        StringAssert.Contains(security, "Creation accepts only the fixed app-owned");
        StringAssert.Contains(security, "Layers drag/drop uses explicit before, after, or inside-group placement");
        StringAssert.Contains(security, "never serialized");
    }

    private static string ReadDocument(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "docs", fileName));

    private static string ReadRepositoryDocument(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "repository",
            fileName));
}
