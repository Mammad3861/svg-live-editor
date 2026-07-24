using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgValidationService
{
    public const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const long MaximumDocumentCharacters = 10_000_000;

    private static readonly HashSet<string> BlockedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script",
        "foreignObject",
        "iframe",
        "object",
        "embed",
        "audio",
        "video",
        "canvas",
        "a"
    };

    private static readonly Regex CssUrlPattern = new(
        "url\\s*\\(\\s*(['\"]?)(?<target>.*?)\\1\\s*\\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public SvgValidationResult Validate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return SvgValidationResult.Invalid("The document is empty.");
        }

        XDocument document;

        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumDocumentCharacters,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };

            using StringReader textReader = new(source);
            using XmlReader xmlReader = XmlReader.Create(textReader, settings);
            document = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            string message = exception.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
                ? "DTD and entity declarations are unsupported and unsafe."
                : exception.Message;

            return SvgValidationResult.Invalid(message, exception.LineNumber, exception.LinePosition);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return SvgValidationResult.Invalid(exception.Message);
        }

        XProcessingInstruction? processingInstruction = document
            .DescendantNodes()
            .OfType<XProcessingInstruction>()
            .FirstOrDefault();

        if (processingInstruction is not null)
        {
            return InvalidAt(processingInstruction, "XML processing instructions are not supported in previews.");
        }

        XElement? root = document.Root;
        if (root is null || !root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            return root is null
                ? SvgValidationResult.Invalid("The document must have an SVG root element.")
                : InvalidAt(root, "The document root must be an svg element.");
        }

        if (!root.Name.NamespaceName.Equals(SvgNamespace, StringComparison.Ordinal))
        {
            return InvalidAt(root, $"The svg root must use the standard namespace: {SvgNamespace}");
        }

        foreach (XElement element in root.DescendantsAndSelf())
        {
            SvgValidationResult? elementResult = ValidateElement(element);
            if (elementResult is not null)
            {
                return elementResult;
            }

            foreach (XAttribute attribute in element.Attributes())
            {
                SvgValidationResult? attributeResult = ValidateAttribute(attribute);
                if (attributeResult is not null)
                {
                    return attributeResult;
                }
            }
        }

        return SvgValidationResult.Valid();
    }

    private static SvgValidationResult? ValidateElement(XElement element)
    {
        if (!element.Name.NamespaceName.Equals(SvgNamespace, StringComparison.Ordinal))
        {
            return InvalidAt(element, $"Element '{element.Name.LocalName}' is outside the SVG namespace.");
        }

        if (BlockedElements.Contains(element.Name.LocalName))
        {
            return InvalidAt(element, $"Element '{element.Name.LocalName}' is not allowed in the secure preview.");
        }

        if (element.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidAt(element, "Style elements are not supported; use safe inline style attributes.");
        }

        return null;
    }

    private static SvgValidationResult? ValidateAttribute(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            return null;
        }

        string localName = attribute.Name.LocalName;

        if (localName.Length > 2 && localName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidAt(attribute, $"Inline event handler '{localName}' is not allowed.");
        }

        if (attribute.Name == XNamespace.Xml + "base")
        {
            return InvalidAt(attribute, "The xml:base attribute is not allowed.");
        }

        if (localName.Equals("href", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            string target = attribute.Value.Trim();
            if (target.Length > 0 && !target.StartsWith('#'))
            {
                return InvalidAt(attribute, $"External or embedded resource reference '{target}' is not allowed.");
            }
        }

        if (ContainsUnsafeCss(attribute.Value))
        {
            return InvalidAt(attribute, $"Attribute '{localName}' contains an external CSS resource.");
        }

        return null;
    }

    private static bool ContainsUnsafeCss(string value)
    {
        if (value.Contains("@import", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (Match match in CssUrlPattern.Matches(value))
        {
            string target = match.Groups["target"].Value.Trim();
            if (!target.StartsWith('#'))
            {
                return true;
            }
        }

        return false;
    }

    private static SvgValidationResult InvalidAt(XObject node, string message)
    {
        if (node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return SvgValidationResult.Invalid(message, lineInfo.LineNumber, lineInfo.LinePosition);
        }

        return SvgValidationResult.Invalid(message);
    }
}
