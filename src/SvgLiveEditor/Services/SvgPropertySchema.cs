using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class SvgPropertySchema
{
    private static readonly string[] DirectionValues = ["", "ltr", "rtl"];
    private static readonly string[] UnicodeBidiValues =
        ["", "normal", "embed", "isolate", "plaintext"];
    private static readonly string[] TextAnchorValues =
        ["", "start", "middle", "end"];
    private static readonly string[] FontWeightValues =
        ["", "normal", "bold", "100", "200", "300", "400", "500",
         "600", "700", "800", "900", "bolder", "lighter"];
    private static readonly string[] FontStyleValues =
        ["", "normal", "italic", "oblique"];

    private static readonly SvgPropertyDefinition[] Common =
    [
        new("id", HelpText: "Optional element identifier used by fragment references."),
        new("fill", HelpText: "Interior paint, such as a color, none, currentColor, or url(#gradient)."),
        new("stroke", HelpText: "Outline paint, such as a color, none, currentColor, or url(#gradient)."),
        new("stroke-width", HelpText: "Width of the element outline in SVG user units."),
        new(
            "opacity",
            RemoveWhenEmpty: true,
            HelpText: "The selected element's own global opacity from 0 (transparent) to 1 (opaque).")
    ];

    private static readonly IReadOnlyDictionary<string, SvgPropertyDefinition[]> ElementSpecific =
        new Dictionary<string, SvgPropertyDefinition[]>(StringComparer.Ordinal)
        {
            ["svg"] =
            [
                Width(),
                Height(),
                new("viewBox", HelpText: "The SVG coordinate rectangle: minimum x, minimum y, width, and height.")
            ],
            ["rect"] =
            [
                X(),
                Y(),
                Width(),
                Height(),
                new("rx", HelpText: "Horizontal corner radius of the rectangle."),
                new("ry", HelpText: "Vertical corner radius of the rectangle.")
            ],
            ["circle"] =
            [
                Cx(),
                Cy(),
                new("r", HelpText: "Radius of the circle in SVG user units.")
            ],
            ["ellipse"] =
            [
                Cx(),
                Cy(),
                new("rx", HelpText: "Horizontal radius of the ellipse."),
                new("ry", HelpText: "Vertical radius of the ellipse.")
            ],
            ["line"] =
            [
                new("x1", HelpText: "Horizontal coordinate of the line's start point."),
                new("y1", HelpText: "Vertical coordinate of the line's start point."),
                new("x2", HelpText: "Horizontal coordinate of the line's end point."),
                new("y2", HelpText: "Vertical coordinate of the line's end point.")
            ],
            ["text"] =
            [
                new("x", HelpText: "Horizontal position of the text anchor."),
                new("y", HelpText: "Vertical position of the text baseline."),
                new(
                    "font-family",
                    RemoveWhenEmpty: true,
                    UsesFontFamilySuggestions: true,
                    HelpText: "Preferred installed font family; existing safe fallback families are preserved."),
                new(
                    "font-size",
                    RemoveWhenEmpty: true,
                    HelpText: "Text size as a positive unitless or px value."),
                OptionalEnum("font-weight", FontWeightValues),
                OptionalEnum("font-style", FontStyleValues),
                OptionalEnum(
                    "direction",
                    DirectionValues,
                    "Base text direction: left-to-right or right-to-left."),
                OptionalEnum(
                    "unicode-bidi",
                    UnicodeBidiValues,
                    "Constrained SVG bidi handling; override values are intentionally unavailable."),
                OptionalEnum(
                    "text-anchor",
                    TextAnchorValues,
                    "Aligns text start, middle, or end to its x coordinate.")
            ],
            ["tspan"] =
            [
                new("x", HelpText: "Horizontal position of the text span anchor."),
                new("y", HelpText: "Vertical position of the text span baseline."),
                OptionalEnum(
                    "direction",
                    DirectionValues,
                    "Base text direction: left-to-right or right-to-left."),
                OptionalEnum(
                    "unicode-bidi",
                    UnicodeBidiValues,
                    "Constrained SVG bidi handling; override values are intentionally unavailable."),
                OptionalEnum(
                    "text-anchor",
                    TextAnchorValues,
                    "Aligns text start, middle, or end to its x coordinate.")
            ],
            ["path"] =
            [
                new(
                    "d",
                    IsReadOnly: true,
                    HelpText: "Path command data; edit it directly in Source.")
            ]
        };

    public static IReadOnlyList<SvgPropertyDefinition> GetProperties(string elementName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

        if (!ElementSpecific.TryGetValue(
                elementName,
                out SvgPropertyDefinition[]? elementProperties))
        {
            return Common;
        }

        return [.. Common, .. elementProperties];
    }

    public static SvgPropertyDefinition? Find(string elementName, string attributeName)
    {
        return GetProperties(elementName).FirstOrDefault(property =>
            property.Name.Equals(attributeName, StringComparison.Ordinal));
    }

    private static SvgPropertyDefinition OptionalEnum(
        string name,
        IReadOnlyList<string> allowedValues,
        string helpText = "") =>
        new(
            name,
            RemoveWhenEmpty: true,
            AllowedValues: allowedValues,
            HelpText: helpText);

    private static SvgPropertyDefinition X() =>
        new("x", HelpText: "Horizontal position in SVG user units.");

    private static SvgPropertyDefinition Y() =>
        new("y", HelpText: "Vertical position in SVG user units.");

    private static SvgPropertyDefinition Width() =>
        new("width", HelpText: "Element width in SVG user units.");

    private static SvgPropertyDefinition Height() =>
        new("height", HelpText: "Element height in SVG user units.");

    private static SvgPropertyDefinition Cx() =>
        new("cx", HelpText: "Horizontal coordinate of the element center.");

    private static SvgPropertyDefinition Cy() =>
        new("cy", HelpText: "Vertical coordinate of the element center.");
}
