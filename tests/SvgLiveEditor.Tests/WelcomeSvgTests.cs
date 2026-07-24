using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class WelcomeSvgTests
{
    [TestMethod]
    public void WelcomeSample_IsOriginalSafeValidUnicodeSvg()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "samples", "welcome.svg");
        string source = File.ReadAllText(path);
        string embeddedSource = new WelcomeSvgProvider().Load();
        SvgValidationService validator = new();

        Models.SvgValidationResult result = validator.Validate(source);

        Assert.IsTrue(result.IsValid, result.Message);
        Assert.AreEqual(source, embeddedSource);
        StringAssert.Contains(source, "سلام SVG");
        StringAssert.Contains(source, "Idea");
        StringAssert.Contains(source, "Edit SVG");
        StringAssert.Contains(source, "Live Preview");
        StringAssert.Contains(source, "Save");
        StringAssert.Contains(source, "opacity=");
        StringAssert.Contains(source, "style=\"fill:#f8fafc\"");
        Assert.IsFalse(source.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("foreignObject", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("https://", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("href=", StringComparison.OrdinalIgnoreCase));
    }
}
