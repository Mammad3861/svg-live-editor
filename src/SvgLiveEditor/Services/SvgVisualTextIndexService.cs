using System.IO;
using System.Xml;
using System.Xml.Linq;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualTextIndexService
{
    public const int MaximumMeasuredTextElements = 32;
    public const int MaximumTextLength = 1_024;
    public const double MinimumResizableFontSize = 1;
    public const double MaximumResizableFontSize = 512;
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
    private readonly string _source;

    private SvgVisualTextIndexService(
        IReadOnlyDictionary<string, XElement> elementsByPath,
        IReadOnlyDictionary<string, SvgElementNode> nodesByPath,
        string source)
    {
        _elementsByPath = elementsByPath;
        _nodesByPath = nodesByPath;
        _source = source;
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
                nodesByPath,
                source);
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
        SvgDirectTextAnalysis analysis = AnalyzeDirectText(element);
        if (!analysis.IsEligible
            || analysis.Content is not SvgDirectTextContent content)
        {
            error = analysis.UnavailableReason;
            return false;
        }
        string text = content.Text;
        if (text.Length == 0)
        {
            error =
                "Empty text can be edited in Properties but has no Preview bounds.";
            return false;
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

    public bool TryCreateResizeSource(
        SvgElementNode element,
        out SvgTextResizeSource? resizeSource,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(element);
        resizeSource = null;
        SvgDirectTextAnalysis directText = AnalyzeDirectText(element);
        if (!directText.IsEligible)
        {
            error = directText.UnavailableReason;
            return false;
        }
        if (SvgVisualStylePolicy.Defines(element, "font-size"))
        {
            error = "Text size is owned by inline style; edit it in Source.";
            return false;
        }

        string? rawValue = element.FindAttribute("font-size")?.RawValue;
        if (rawValue is null)
        {
            error =
                "Text resize requires a direct font-size attribute on the selected text.";
            return false;
        }
        if (!SvgXmlAttributeValueDecoder.TryDecode(rawValue, out string decoded)
            || !SvgVisualLengthParser.TryParse(
                decoded,
                defaultValue: 0,
                out double fontSize,
                out string suffix)
            || (suffix.Length != 0
                && !suffix.Equals("px", StringComparison.Ordinal))
            || fontSize < MinimumResizableFontSize
            || fontSize > MaximumResizableFontSize)
        {
            error =
                $"Text resize requires a direct unitless or px font-size from {MinimumResizableFontSize} to {MaximumResizableFontSize}.";
            return false;
        }

        resizeSource = new SvgTextResizeSource(fontSize, suffix);
        error = null;
        return true;
    }

    public SvgDirectTextAnalysis AnalyzeDirectText(SvgElementNode element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!element.Name.Equals("text", StringComparison.Ordinal))
        {
            return SvgDirectTextAnalysis.Rejected(
                "Text content is available only for a direct text element.");
        }
        if (!_elementsByPath.TryGetValue(
                element.StructuralPath,
                out XElement? xmlElement))
        {
            return SvgDirectTextAnalysis.Rejected(
                "The text source mapping is unavailable.");
        }
        if (element.Children.Count != 0 || xmlElement.Elements().Any())
        {
            return SvgDirectTextAnalysis.Rejected(
                "Direct text editing does not support tspan, textPath, animation, or nested elements.");
        }
        if (xmlElement.Nodes().Any(node =>
                node is not XText || node is XCData))
        {
            return SvgDirectTextAnalysis.Rejected(
                "Direct text editing requires one plain text-content span without CDATA or comments.");
        }
        if (HasExplicitXmlSpace(element))
        {
            return SvgDirectTextAnalysis.Rejected(
                "Direct text editing does not support inherited or direct xml:space whitespace ownership.");
        }

        string text = string.Concat(
            xmlElement.Nodes().OfType<XText>().Select(node => node.Value));
        if (text.Length > MaximumTextLength || text.Any(char.IsControl))
        {
            return SvgDirectTextAnalysis.Rejected(
                "Direct text editing requires bounded single-line plain text without control characters.");
        }
        if (SvgVisualStylePolicy.IsDefinitelyNotRendered(
                element,
                _nodesByPath))
        {
            return SvgDirectTextAnalysis.Rejected(
                "Hidden text cannot be edited or resized from Preview.");
        }

        string? visualError = SvgVisualGeometryIndexService
            .GetUnsupportedReason(element, _nodesByPath);
        if (visualError is not null)
        {
            return SvgDirectTextAnalysis.Rejected(visualError);
        }
        foreach (SvgElementNode current in EnumerateSelfAndAncestors(element))
        {
            if (HasUnsupportedLayout(current))
            {
                return SvgDirectTextAnalysis.Rejected(
                    "Direct text editing does not support complex text layout, stroke, or baseline effects.");
            }
        }
        if (!TryFindContentSpan(element, out SourceSpan contentSpan)
            || _source.AsSpan(
                    contentSpan.Start,
                    contentSpan.Length).IndexOf('<') >= 0)
        {
            return SvgDirectTextAnalysis.Rejected(
                "An exact direct text-content span could not be proven safe.");
        }

        return SvgDirectTextAnalysis.Eligible(
            new SvgDirectTextContent(element, contentSpan, text));
    }

    private bool TryFindContentSpan(
        SvgElementNode element,
        out SourceSpan contentSpan)
    {
        contentSpan = default;
        if (element.FullSpan == element.StartTagSpan
            || element.StartTagSpan.End > element.FullSpan.End
            || element.FullSpan.End > _source.Length)
        {
            return false;
        }

        int closingStart = _source.LastIndexOf(
            "</",
            element.FullSpan.End - 1,
            element.FullSpan.Length,
            StringComparison.Ordinal);
        if (closingStart < element.StartTagSpan.End)
        {
            return false;
        }
        int cursor = closingStart + 2;
        int nameStart = cursor;
        while (cursor < element.FullSpan.End
            && !char.IsWhiteSpace(_source[cursor])
            && _source[cursor] != '>')
        {
            cursor++;
        }
        if (!_source.AsSpan(nameStart, cursor - nameStart)
                .SequenceEqual(element.QualifiedName.AsSpan()))
        {
            return false;
        }
        while (cursor < element.FullSpan.End
            && char.IsWhiteSpace(_source[cursor]))
        {
            cursor++;
        }
        if (cursor != element.FullSpan.End - 1 || _source[cursor] != '>')
        {
            return false;
        }

        contentSpan = new SourceSpan(
            element.StartTagSpan.End,
            closingStart - element.StartTagSpan.End);
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

    private bool HasExplicitXmlSpace(SvgElementNode element)
    {
        foreach (SvgElementNode current in EnumerateSelfAndAncestors(element))
        {
            if (_elementsByPath.TryGetValue(
                    current.StructuralPath,
                    out XElement? xmlElement)
                && xmlElement.Attribute(XNamespace.Xml + "space") is not null)
            {
                return true;
            }
        }

        return false;
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
