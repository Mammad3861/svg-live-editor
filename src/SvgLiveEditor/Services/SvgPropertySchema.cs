using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class SvgPropertySchema
{
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
            ["text"] = [new("x"), new("y")],
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
}
