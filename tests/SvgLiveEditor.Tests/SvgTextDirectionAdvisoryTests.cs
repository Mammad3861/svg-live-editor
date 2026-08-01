using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTextDirectionAdvisoryTests
{
    private readonly SvgTextDirectionAdvisoryService _service = new();

    [TestMethod]
    [DataRow("بهروز", "ltr", SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection)]
    [DataRow("بهروز", "rtl", null)]
    [DataRow("Hello", "rtl", SvgTextDirectionAdvisoryService.LtrTextWithRtlDirection)]
    [DataRow("Hello", "ltr", null)]
    [DataRow("بهروز Hello", "ltr", SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection)]
    [DataRow("Hello بهروز", "rtl", SvgTextDirectionAdvisoryService.LtrTextWithRtlDirection)]
    public void WarningUsesTheFirstStrongCharacterWithoutRewritingSource(
        string text,
        string direction,
        string? expected)
    {
        Assert.AreEqual(expected, _service.GetWarning(text, direction));
    }

    [TestMethod]
    [DataRow("123 (۱۲۳): بهروز!", "ltr", SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection)]
    [DataRow("۱۲۳، 456: Hello?", "rtl", SvgTextDirectionAdvisoryService.LtrTextWithRtlDirection)]
    [DataRow("۱۲۳ (456): .?!", "ltr", null)]
    [DataRow("۱۲۳ (456): .?!", "rtl", null)]
    public void DigitsAndNeutralPunctuationDoNotBecomeStrongDirection(
        string text,
        string direction,
        string? expected)
    {
        Assert.AreEqual(expected, _service.GetWarning(text, direction));
    }

    [TestMethod]
    public void WarningClearsWhenDirectionOrLeadingContentIsCorrected()
    {
        Assert.AreEqual(
            SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection,
            _service.GetWarning("بهروز", "ltr"));
        Assert.IsNull(_service.GetWarning("بهروز", "rtl"));
        Assert.IsNull(_service.GetWarning("Hello بهروز", "ltr"));
    }
}
