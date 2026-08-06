using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public static class SvgLayerPolicy
{
    private static readonly HashSet<string> ArtworkNames =
        new(StringComparer.Ordinal)
        {
            "rect",
            "circle",
            "ellipse",
            "line",
            "text",
            "path",
            "polygon",
            "polyline"
        };

    private static readonly HashSet<string> DefinitionContainerNames =
        new(StringComparer.Ordinal)
        {
            "defs",
            "clipPath",
            "filter",
            "linearGradient",
            "marker",
            "mask",
            "metadata",
            "pattern",
            "radialGradient",
            "symbol"
        };

    public static bool IsLayerElement(string elementName) =>
        elementName.Equals("g", StringComparison.Ordinal)
        || ArtworkNames.Contains(elementName);

    public static bool IsGroup(string elementName) =>
        elementName.Equals("g", StringComparison.Ordinal);

    public static bool IsArtwork(string elementName) =>
        ArtworkNames.Contains(elementName);

    public static bool IsDefinitionContainer(string elementName) =>
        DefinitionContainerNames.Contains(elementName);

    public static bool IsInsideDefinitionContainer(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        for (SvgElementNode? current = element;
             current is not null;
             current = document.FindParent(current))
        {
            if (IsDefinitionContainer(current.Name))
            {
                return true;
            }
        }

        return false;
    }
}
