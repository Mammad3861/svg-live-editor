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
        new("id"),
        new("fill"),
        new("stroke"),
        new("stroke-width"),
        new("opacity")
    ];

    private static readonly IReadOnlyDictionary<string, SvgPropertyDefinition[]> ElementSpecific =
        new Dictionary<string, SvgPropertyDefinition[]>(StringComparer.Ordinal)
        {
            ["svg"] = [new("width"), new("height"), new("viewBox")],
            ["rect"] =
            [
                new("x"),
                new("y"),
                new("width"),
                new("height"),
                new("rx"),
                new("ry")
            ],
            ["circle"] = [new("cx"), new("cy"), new("r")],
            ["ellipse"] = [new("cx"), new("cy"), new("rx"), new("ry")],
            ["line"] = [new("x1"), new("y1"), new("x2"), new("y2")],
            ["text"] =
            [
                new("x"),
                new("y"),
                new(
                    "font-family",
                    RemoveWhenEmpty: true,
                    UsesFontFamilySuggestions: true),
                new("font-size", RemoveWhenEmpty: true),
                OptionalEnum("font-weight", FontWeightValues),
                OptionalEnum("font-style", FontStyleValues),
                OptionalEnum("direction", DirectionValues),
                OptionalEnum("unicode-bidi", UnicodeBidiValues),
                OptionalEnum("text-anchor", TextAnchorValues)
            ],
            ["tspan"] =
            [
                new("x"),
                new("y"),
                OptionalEnum("direction", DirectionValues),
                OptionalEnum("unicode-bidi", UnicodeBidiValues),
                OptionalEnum("text-anchor", TextAnchorValues)
            ],
            ["path"] = [new("d", IsReadOnly: true)]
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
        IReadOnlyList<string> allowedValues) =>
        new(
            name,
            RemoveWhenEmpty: true,
            AllowedValues: allowedValues);
}
