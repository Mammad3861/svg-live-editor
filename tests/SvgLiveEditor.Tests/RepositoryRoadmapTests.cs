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
            "v0.9.0 — Visual Authoring (shipped standalone release)",
            "Insert bounded basic SVG elements and empty groups",
            "safe deterministic ID/reference remapping",
            "Explicit conservative move into, out of, and between existing groups",
            "v0.10.0 — Visual Composition",
            "Stage 0 authoring stabilization (completed)",
            "valid Preview visible and latest-wins",
            "explicit: SVG root or the selected group/sibling context",
            "accessible native Layers disclosure",
            "Stage 1 multi-selection and layout tools (implemented)",
            "Bounded Preview/Layers multi-selection",
            "Atomic movement and nudge",
            "Conservative same-parent contiguous Group",
            "Six visual-bounds alignment commands",
            "Optional basic sibling/object and canvas-center snapping",
            "Later v0.10 polish (not part of Stage 1)",
            "safe basic path bounding-box resize",
            "partially positioned or resized outside the root SVG viewBox/canvas",
            "360-degree rotation",
            "v1.0.0 — Stable Release / stabilization",
            "Reliability and data-loss review",
            "Persistence and recovery validation",
            "Advanced selection visual redesign",
            "Real-time WYSIWYG manipulation",
            "actual artwork follow the pointer smoothly during Move and Resize",
            "source authoritative and commit it once on pointer release",
            "Escape cancels",
            "one logical Undo operation",
            "without rebuilding or navigating the full Preview for every pointer movement",
            "after the initial v1 release",
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
            "v0.9.0 — Visual Authoring (انتشار مستقل عرضه‌شده)",
            "درج محدود و امن عناصر پایهٔ SVG و گروه خالی",
            "بازنویسی امن و قطعی ID/reference",
            "انتقال صریح و محافظه‌کارانه به داخل، خارج و میان گروه‌های موجود",
            "v0.10.0 — Visual Composition",
            "مرحلهٔ ۰ برای پایدارسازی authoring (تکمیل‌شده)",
            "Preview معتبر و latest-wins",
            "ریشهٔ SVG یا گروه/زمینهٔ هم‌سطح انتخاب‌شده",
            "disclosure بومی و دسترس‌پذیر در Layers",
            "مرحلهٔ ۱ انتخاب چندگانه و ابزارهای layout (پیاده‌سازی‌شده)",
            "انتخاب چندگانهٔ محدود در Preview/Layers",
            "حرکت و Nudge اتمی",
            "Group محافظه‌کارانه برای siblingهای پیوستهٔ هم‌والد",
            "شش فرمان هم‌ترازی بر پایهٔ bounds دیداری",
            "snapping پایه و اختیاری به sibling/شیء و مرکز canvas",
            "پرداخت بعدی v0.10 (خارج از مرحلهٔ ۱)",
            "bounding-box برای path",
            "بخشی بیرون viewBox/canvas ریشه",
            "rotation کامل ۳۶۰ درجه",
            "v1.0.0 — انتشار پایدار / پایدارسازی",
            "خطر از دست رفتن داده",
            "اعتبارسنجی ماندگاری و بازیابی",
            "بازطراحی پیشرفتهٔ ظاهر انتخاب",
            "دست‌کاری بلادرنگ WYSIWYG",
            "هنگام Move و Resize خود اثر",
            "Escape عملیات را لغو کند",
            "یک Undo منطقی",
            "navigation کامل Preview",
            "پس از انتشار اولیهٔ v1",
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
                     "Version 0.10.0 Stage 1 implements bounded multi-selection",
                     "atomic multi-object movement/nudge",
                     "conservative Group/Ungroup",
                     "equal-gap distribution",
                     "optional object/canvas-center snapping",
                     "within 4 CSS pixels",
                     "Use `Ctrl`+left-drag",
                     "explicit before/after/inside cross-parent operation",
                     "session-only",
                     "Stage 0 latest-wins mounted preview",
                     "Unsafe or ambiguous operations fail closed"
                 })
        {
            StringAssert.Contains(english, phrase);
        }
        foreach (string phrase in new[]
                 {
                     "مرحلهٔ ۱ نسخهٔ ۰٫۱۰٫۰ انتخاب چندگانهٔ محدود",
                     "حرکت/Nudge اتمی چند شیء",
                     "Group/Ungroup محافظه‌کارانه",
                     "توزیع با فاصلهٔ برابر",
                     "snapping اختیاری شیء/مرکز canvas",
                     "آستانهٔ ۴ پیکسل CSS",
                     "با `Ctrl` همراه کشیدن چپ",
                     "مسیر صریح before/after/inside برای تغییر والد",
                     "قفل فقط در نشست جاری",
                     "پایهٔ latest-wins و source-authoritative مرحلهٔ ۰",
                     "عملیات ناامن یا مبهم fail closed است"
                 })
        {
            StringAssert.Contains(persian, phrase);
        }
        StringAssert.Contains(security, "Layers and groups boundary");
        StringAssert.Contains(
            security,
            "Direct PNG drag requires explicit `Ctrl`+primary drag");
        StringAssert.Contains(security, "Creation accepts only the fixed app-owned");
        StringAssert.Contains(security, "requires an explicit SVG-root or selected-context destination");
        StringAssert.Contains(security, "Friendly layer rename edits only an optional bounded `data-name` attribute");
        StringAssert.Contains(
            security,
            "Layers drag/drop remains the only explicit before/after/inside cross-parent path");
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
