using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgOpacityService
{
    private static readonly HashSet<string> AnimationElementNames =
        new(StringComparer.Ordinal)
        {
            "animate",
            "animateMotion",
            "animateTransform",
            "set"
        };

    private static readonly string[] ExcludedVisualEffectAttributes =
    [
        "clip-path",
        "mask",
        "filter",
        "marker",
        "marker-start",
        "marker-mid",
        "marker-end",
        "transform"
    ];

    private readonly SvgAttributeEditService _attributeEditService = new();

    public SvgOpacityControlState Analyze(
        SvgDocumentIndex document,
        SvgElementNode element,
        string? source = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        if (!SvgLayerOrderService.IsPaintableElement(element.Name))
        {
            return SvgOpacityControlState.Hidden;
        }

        if (element.Name is "path" or "polygon" or "polyline"
            && (source is null
                || new SvgVisualGeometryIndexService().Build(
                        document,
                        new SvgCanvasSizeReader().Read(source),
                        source)
                    .FindElement(element.Identity)
                    is not { IsSelectable: true }))
        {
            return Disabled(
                "Opacity requires reliable conservative bounds for this element.");
        }

        if (HasStyleOpacity(element, out bool ambiguousStyle))
        {
            return Disabled("Opacity is controlled by inline style and is not edited by this control.");
        }
        if (ambiguousStyle)
        {
            return Disabled("Opacity is unavailable because the inline style is ambiguous.");
        }
        if (element.Children.Any(child => AnimationElementNames.Contains(child.Name)))
        {
            return Disabled("Opacity is unavailable for animated elements.");
        }

        foreach (SvgElementNode current in EnumerateSelfAndAncestors(document, element))
        {
            if (HasExcludedVisualEffects(current))
            {
                return Disabled("Opacity is unavailable for transformed, clipped, masked, filtered, or marker-decorated artwork.");
            }
        }

        SvgAttributeSpan? attribute = element.FindAttribute("opacity");
        double percent = 100;
        double parsedOpacity = 1;
        if (attribute is not null
            && (!SvgXmlAttributeValueDecoder.TryDecode(attribute.RawValue, out string decoded)
                || !double.TryParse(
                    decoded.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsedOpacity)
                || !double.IsFinite(parsedOpacity)
                || parsedOpacity is < 0 or > 1))
        {
            return Disabled("The existing opacity value is malformed or outside 0 to 1.");
        }
        else if (attribute is not null)
        {
            percent = parsedOpacity * 100;
        }

        bool hasAncestorOpacity = EnumerateSelfAndAncestors(document, element)
            .Skip(1)
            .Any(DefinesOpacity);
        return new SvgOpacityControlState(
            true,
            true,
            percent,
            Advisory: hasAncestorOpacity
                ? "An ancestor also has opacity, so the effective result may be lower."
                : null);
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode element,
        double percent)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(percent) || percent is < 0 or > 100)
        {
            return SvgAttributeEditResult.Invalid(
                "Opacity percentage must be between 0 and 100.");
        }

        SvgOpacityControlState state = Analyze(document, element, source);
        if (!state.IsEnabled)
        {
            return SvgAttributeEditResult.Invalid(
                state.UnavailableReason ?? "Opacity is unavailable for this element.");
        }

        string value = Math.Abs(percent - 100) < 0.0000001
            ? string.Empty
            : (percent / 100).ToString("0.####", CultureInfo.InvariantCulture);
        return _attributeEditService.CreateEdit(
            source,
            element,
            "opacity",
            value);
    }

    private static SvgOpacityControlState Disabled(string reason) =>
        new(true, false, 100, reason);

    private static bool HasStyleOpacity(
        SvgElementNode element,
        out bool ambiguous)
    {
        ambiguous = SvgVisualStylePolicy.HasAmbiguousSyntax(element);
        if (ambiguous)
        {
            return false;
        }

        string? rawStyle = element.FindAttribute("style")?.RawValue;
        if (rawStyle is null
            || !SvgXmlAttributeValueDecoder.TryDecode(rawStyle, out string style))
        {
            return false;
        }

        return style.Split(';').Any(declaration =>
        {
            int separator = declaration.IndexOf(':');
            return separator > 0
                && declaration[..separator].Trim().Equals(
                    "opacity",
                    StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool DefinesOpacity(SvgElementNode element) =>
        element.FindAttribute("opacity") is not null
        || HasStyleOpacity(element, out _);

    private static bool HasExcludedVisualEffects(SvgElementNode element)
    {
        foreach (string attributeName in ExcludedVisualEffectAttributes)
        {
            if (!string.IsNullOrWhiteSpace(
                    element.FindAttribute(attributeName)?.RawValue)
                || SvgVisualStylePolicy.Defines(element, attributeName))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<SvgElementNode> EnumerateSelfAndAncestors(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        SvgElementNode? current = element;
        while (current is not null)
        {
            yield return current;
            current = document.Elements.FirstOrDefault(candidate =>
                candidate.Children.Any(child => ReferenceEquals(child, current)));
        }
    }
}
