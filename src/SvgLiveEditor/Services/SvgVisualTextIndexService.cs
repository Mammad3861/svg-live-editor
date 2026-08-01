using System.IO;
using System.Xml;
using System.Xml.Linq;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualTextIndexService
{
    public const int MaximumMeasuredTextElements = 32;
    public const int MaximumTextLength = 1_024;
    private const long MaximumDocumentCharacters = 10_000_000;

    private static readonly HashSet<string> UnsupportedLayoutProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "font",
            "font-stretch",
            "font-variant",
            "font-feature-settings",
            "font-variation-settings",
            "dx",
            "dy",
            "rotate",
            "letter-spacing",
            "word-spacing",
            "text-decoration",
            "textLength",
            "lengthAdjust",
            "writing-mode",
            "glyph-orientation-horizontal",
            "glyph-orientation-vertical",
            "dominant-baseline",
            "alignment-baseline",
            "baseline-shift",
            "paint-order"
        };

    private readonly IReadOnlyDictionary<string, XElement> _elementsByPath;
    private readonly IReadOnlyDictionary<string, SvgElementNode> _nodesByPath;

    private SvgVisualTextIndexService(
        IReadOnlyDictionary<string, XElement> elementsByPath,
        IReadOnlyDictionary<string, SvgElementNode> nodesByPath)
    {
        _elementsByPath = elementsByPath;
        _nodesByPath = nodesByPath;
    }

    public static bool TryCreate(
        SvgDocumentIndex documentIndex,
        string source,
        out SvgVisualTextIndexService? service)
    {
        ArgumentNullException.ThrowIfNull(documentIndex);
        ArgumentNullException.ThrowIfNull(source);
        service = null;
        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumDocumentCharacters,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false
            };
            using StringReader textReader = new(source);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            XDocument xml = XDocument.Load(
                reader,
                LoadOptions.PreserveWhitespace);
            XElement? root = xml.Root;
            if (root is null)
            {
                return false;
            }

            Dictionary<string, XElement> elementsByPath =
                new(StringComparer.Ordinal);
            IndexElements(root, "0", elementsByPath);
            Dictionary<string, SvgElementNode> nodesByPath =
                documentIndex.Elements.ToDictionary(
                    element => element.StructuralPath,
                    StringComparer.Ordinal);
            service = new SvgVisualTextIndexService(
                elementsByPath,
                nodesByPath);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public bool TryCreateMeasurement(
        SvgElementNode element,
        int measurementIndex,
        out SvgVisualTextMeasurementSpec? measurement,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(element);
        measurement = null;
        error = null;
        if (measurementIndex < 0
            || measurementIndex >= MaximumMeasuredTextElements)
        {
            error =
                $"Only the first {MaximumMeasuredTextElements} simple text elements are available to the Select tool.";
            return false;
        }
        if (!_elementsByPath.TryGetValue(
                element.StructuralPath,
                out XElement? xmlElement))
        {
            error = "The text source mapping is unavailable.";
            return false;
        }
        if (element.Children.Count != 0
            || xmlElement.Elements().Any())
        {
            error =
                "Visual text editing does not support tspan, textPath, animation, or nested elements.";
            return false;
        }
        if (xmlElement.Nodes().Any(node =>
                node is not XText || node is XCData))
        {
            error =
                "Visual text editing requires direct plain text content only.";
            return false;
        }

        string text = string.Concat(
            xmlElement.Nodes().OfType<XText>().Select(node => node.Value));
        if (text.Length == 0
            || text.Length > MaximumTextLength
            || !text.Equals(text.Trim(), StringComparison.Ordinal)
            || text.Any(char.IsControl)
            || text.Contains("  ", StringComparison.Ordinal)
            || xmlElement.Attribute(XNamespace.Xml + "space") is not null)
        {
            error =
                "Visual text editing requires short direct text without control characters or complex whitespace.";
            return false;
        }

        foreach (SvgElementNode current in EnumerateSelfAndAncestors(element))
        {
            if (HasUnsupportedLayout(current))
            {
                error =
                    "Visual text editing does not support complex text layout, stroke, or baseline effects.";
                return false;
            }
        }

        if (!TryCoordinate(element, "x", out double x)
            || !TryCoordinate(element, "y", out double y))
        {
            error =
                "Visual text editing requires one finite unitless or px x and y value.";
            return false;
        }

        string fontFamily = ResolvePresentationValue(
            element,
            "font-family",
            "serif");
        if (SvgFontFamilyValueValidator.Validate(fontFamily) is not null)
        {
            error =
                "Visual text editing requires a bounded local font-family fallback stack.";
            return false;
        }

        string fontSizeValue = ResolvePresentationValue(
            element,
            "font-size",
            "16");
        if (!SvgVisualLengthParser.TryParse(
                fontSizeValue,
                16,
                out double fontSize,
                out _)
            || fontSize <= 0)
        {
            error =
                "Visual text editing requires a positive unitless or px font-size.";
            return false;
        }

        string fontWeight = ResolvePresentationValue(
            element,
            "font-weight",
            "normal").Trim().ToLowerInvariant();
        string fontStyle = ResolvePresentationValue(
            element,
            "font-style",
            "normal").Trim().ToLowerInvariant();
        string textAnchor = ResolvePresentationValue(
            element,
            "text-anchor",
            "start").Trim().ToLowerInvariant();
        string direction = ResolvePresentationValue(
            element,
            "direction",
            "ltr").Trim().ToLowerInvariant();
        string unicodeBidi = ResolvePresentationValue(
            element,
            "unicode-bidi",
            "normal").Trim().ToLowerInvariant();
        if (!IsSupportedFontWeight(fontWeight)
            || fontStyle is not ("normal" or "italic" or "oblique")
            || textAnchor is not ("start" or "middle" or "end")
            || direction is not ("ltr" or "rtl")
            || unicodeBidi is not (
                "normal" or "embed" or "isolate" or "plaintext"))
        {
            error =
                "Visual text editing cannot reliably measure the current typography or bidi settings.";
            return false;
        }

        measurement = new SvgVisualTextMeasurementSpec(
            measurementIndex,
            text,
            x,
            y,
            fontSize,
            fontFamily,
            fontWeight,
            fontStyle,
            textAnchor,
            direction,
            unicodeBidi);
        return true;
    }

    private bool HasUnsupportedLayout(SvgElementNode element)
    {
        if (SvgVisualStylePolicy.HasAmbiguousSyntax(element))
        {
            return true;
        }

        foreach (string property in UnsupportedLayoutProperties)
        {
            if (HasNonEmptyAttribute(element, property)
                || SvgVisualStylePolicy.Defines(element, property))
            {
                return true;
            }
        }

        string? stroke = ReadPresentationValue(element, "stroke");
        return stroke is not null
            && !stroke.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolvePresentationValue(
        SvgElementNode element,
        string propertyName,
        string defaultValue)
    {
        foreach (SvgElementNode current in EnumerateSelfAndAncestors(element))
        {
            string? value = ReadPresentationValue(current, propertyName);
            if (value is null)
            {
                continue;
            }

            string normalized = value.Trim();
            if (normalized.Equals("inherit", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("unset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (normalized.Equals("initial", StringComparison.OrdinalIgnoreCase))
            {
                return defaultValue;
            }
            return normalized;
        }

        return defaultValue;
    }

    private static string? ReadPresentationValue(
        SvgElementNode element,
        string propertyName)
    {
        return SvgVisualStylePolicy.ReadPresentationValue(
            element,
            propertyName);
    }

    private IEnumerable<SvgElementNode> EnumerateSelfAndAncestors(
        SvgElementNode element)
    {
        SvgElementNode current = element;
        while (true)
        {
            yield return current;
            int separator = current.StructuralPath.LastIndexOf('/');
            if (separator < 0
                || !_nodesByPath.TryGetValue(
                    current.StructuralPath[..separator],
                    out current!))
            {
                yield break;
            }
        }
    }

    private static bool TryCoordinate(
        SvgElementNode element,
        string name,
        out double value)
    {
        value = 0;
        string? rawValue = element.FindAttribute(name)?.RawValue;
        return rawValue is not null
            && SvgVisualLengthParser.TryParse(
                rawValue,
                0,
                out value,
                out _);
    }

    private static bool IsSupportedFontWeight(string value) =>
        value is "normal" or "bold"
        || (value.Length == 3
            && value[0] is >= '1' and <= '9'
            && value[1] == '0'
            && value[2] == '0');

    private static bool HasNonEmptyAttribute(
        SvgElementNode element,
        string name) =>
        !string.IsNullOrWhiteSpace(element.FindAttribute(name)?.RawValue);

    private static void IndexElements(
        XElement element,
        string path,
        IDictionary<string, XElement> destination)
    {
        destination.Add(path, element);
        int childIndex = 0;
        foreach (XElement child in element.Elements())
        {
            IndexElements(child, $"{path}/{childIndex}", destination);
            childIndex++;
        }
    }
}
