using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgPropertyHelpTests
{
    [TestMethod]
    public void EveryRequiredCommonAndGeometryPropertyHasConciseHelp()
    {
        (string Element, string Property)[] required =
        [
            ("rect", "x"), ("rect", "y"), ("rect", "width"),
            ("rect", "height"), ("rect", "rx"), ("rect", "ry"),
            ("circle", "cx"), ("circle", "cy"), ("circle", "r"),
            ("ellipse", "rx"), ("ellipse", "ry"),
            ("line", "x1"), ("line", "y1"),
            ("line", "x2"), ("line", "y2"),
            ("rect", "fill"), ("rect", "stroke"),
            ("rect", "stroke-width"), ("rect", "opacity"),
            ("text", "font-family"), ("text", "font-size"),
            ("text", "text-anchor"), ("text", "direction"),
            ("text", "unicode-bidi")
        ];

        foreach ((string element, string property) in required)
        {
            SvgPropertyDefinition definition =
                SvgPropertySchema.Find(element, property)!;
            Assert.IsFalse(string.IsNullOrWhiteSpace(definition.HelpText),
                $"{element}.{property}");
            Assert.IsTrue(definition.HelpText.Length <= 140,
                $"{element}.{property}");
        }
    }

    [TestMethod]
    public void RadiusAndTextBaselineHelpIsContextSpecific()
    {
        StringAssert.Contains(
            SvgPropertySchema.Find("rect", "rx")!.HelpText,
            "corner radius");
        StringAssert.Contains(
            SvgPropertySchema.Find("ellipse", "rx")!.HelpText,
            "Horizontal radius");
        StringAssert.Contains(
            SvgPropertySchema.Find("rect", "ry")!.HelpText,
            "corner radius");
        StringAssert.Contains(
            SvgPropertySchema.Find("ellipse", "ry")!.HelpText,
            "Vertical radius");
        StringAssert.Contains(
            SvgPropertySchema.Find("text", "y")!.HelpText,
            "baseline");
    }

    [TestMethod]
    public void PropertyControlsExposeBoundTooltipsAndScreenReaderHelp()
    {
        string xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "MainWindow.xaml"));

        StringAssert.Contains(xaml, "ToolTip=\"{Binding HelpText}\"");
        StringAssert.Contains(
            xaml,
            "AutomationProperties.HelpText=\"{Binding HelpText}\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"{Binding Name}\"");
    }

    [TestMethod]
    public void PropertyHelpHasOneReusableVisibleKeyboardAccessibleAffordance()
    {
        string xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "MainWindow.xaml"));
        string inspector = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "MainWindow.Inspector.cs"));

        StringAssert.Contains(xaml, "x:Key=\"InspectorHelpButtonStyle\"");
        StringAssert.Contains(xaml, "Property=\"Content\" Value=\"i\"");
        StringAssert.Contains(xaml, "IsTabStop\" Value=\"True\"");
        StringAssert.Contains(
            xaml,
            "AutomationProperties.Name=\"{Binding Name, StringFormat={}{0} property help}\"");
        StringAssert.Contains(xaml, "<ToolTip Content=\"{Binding HelpText}\" />");
        StringAssert.Contains(
            inspector,
            "OnInspectorHelpGotKeyboardFocus");
        StringAssert.Contains(inspector, "toolTip.IsOpen = true");
        StringAssert.Contains(inspector, "toolTip.IsOpen = false");
    }

    [TestMethod]
    public void LayerAndOpacitySectionsStayCompactAndExposeVisibleHelp()
    {
        string xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ui",
            "MainWindow.xaml"));

        StringAssert.Contains(
            xaml,
            "AutomationProperties.Name=\"Layer ordering information\"");
        StringAssert.Contains(
            xaml,
            "<ToolTip Content=\"{Binding Inspector.LayerPosition.BoundaryExplanation}\" />");
        Assert.IsFalse(xaml.Split('\n').Any(line => line.Trim().Equals(
            "Text=\"{Binding Inspector.LayerPosition.BoundaryExplanation}\"",
            StringComparison.Ordinal)));
        StringAssert.Contains(xaml, "x:Key=\"InspectorOptionalMessageStyle\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Opacity help\"");
        StringAssert.Contains(xaml, "MinHeight=\"20\"");
        StringAssert.Contains(xaml, "Width=\"52\"");
    }
}
