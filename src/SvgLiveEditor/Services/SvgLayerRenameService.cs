using System.Net;
using System.Text;
using System.Xml;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayerRenameService
{
    public const string AttributeName = "data-name";
    public const int MaximumNameLength = 128;

    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAuthoringAvailability GetAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? element,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        if (element is null
            || !SvgLayerPolicy.IsLayerElement(element.Name)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, element))
        {
            return new SvgAuthoringAvailability(
                false,
                "Select visual artwork or a group in Layers to name it.");
        }
        if (!SvgSourceMutationUtilities.IsCurrentElement(source, element))
        {
            return new SvgAuthoringAvailability(
                false,
                "The source changed; select the layer again.");
        }
        if (isEffectivelyLocked?.Invoke(element) == true)
        {
            return new SvgAuthoringAvailability(
                false,
                "Unlock the layer and its parent group before naming it.");
        }

        return new SvgAuthoringAvailability(true);
    }

    public SvgAuthoringEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? element,
        string friendlyName,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(friendlyName);

        SvgAuthoringAvailability availability = GetAvailability(
            source,
            document,
            element,
            isEffectivelyLocked);
        if (!availability.CanExecute || element is null)
        {
            return SvgAuthoringEditResult.Invalid(
                availability.UnavailableReason ?? "The layer cannot be named.");
        }

        string? validationError = ValidateFriendlyName(friendlyName);
        if (validationError is not null)
        {
            return SvgAuthoringEditResult.Invalid(validationError);
        }

        SvgAttributeSpan? existing = element.FindAttribute(AttributeName);
        string currentName = DecodeFriendlyName(existing?.RawValue);
        if (friendlyName.Equals(currentName, StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid("The layer name is unchanged.");
        }

        SourceTextEdit edit;
        if (existing is not null)
        {
            if (!IsCurrentAttribute(source, existing))
            {
                return SvgAuthoringEditResult.Invalid(
                    "The source changed; select the layer again.");
            }

            if (friendlyName.Length == 0)
            {
                int start = existing.NameSpan.Start;
                if (start > element.StartTagSpan.Start
                    && source[start - 1] is ' ' or '\t')
                {
                    start--;
                }
                int end = existing.ValueSpan.End + 1;
                if (end > source.Length || source[end - 1] != existing.Quote)
                {
                    return SvgAuthoringEditResult.Invalid(
                        "The source changed; select the layer again.");
                }
                edit = new SourceTextEdit(start, end - start, string.Empty);
            }
            else
            {
                edit = new SourceTextEdit(
                    existing.ValueSpan.Start,
                    existing.ValueSpan.Length,
                    EscapeAttributeValue(friendlyName, existing.Quote));
            }
        }
        else
        {
            if (friendlyName.Length == 0)
            {
                return SvgAuthoringEditResult.Invalid(
                    "This layer has no friendly name to clear.");
            }
            int insertion;
            try
            {
                insertion = FindAttributeInsertionOffset(
                    source,
                    element.StartTagSpan);
            }
            catch (InvalidOperationException)
            {
                return SvgAuthoringEditResult.Invalid(
                    "The source changed; select the layer again.");
            }
            string leadingSpace = insertion > 0
                && char.IsWhiteSpace(source[insertion - 1])
                    ? string.Empty
                    : " ";
            edit = new SourceTextEdit(
                insertion,
                0,
                $"{leadingSpace}{AttributeName}=\"{EscapeAttributeValue(friendlyName, '"')}\"");
        }

        string candidate = edit.Apply(source);
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        if (!validation.IsValid || rebuilt.Document is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The layer name would make the SVG invalid: {validation.Message}");
        }

        SvgElementNode? renamed = rebuilt.Document.FindBestMatch(element.Identity);
        if (renamed is null
            || !DecodeFriendlyName(
                    renamed.FindAttribute(AttributeName)?.RawValue)
                .Equals(friendlyName, StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid(
                "The named layer could not be identified safely.");
        }

        return SvgAuthoringEditResult.Success(edit, renamed.Identity);
    }

    public static string DecodeFriendlyName(string? rawValue) =>
        rawValue is null ? string.Empty : WebUtility.HtmlDecode(rawValue);

    private static string? ValidateFriendlyName(string value)
    {
        if (value.EnumerateRunes().Count() > MaximumNameLength)
        {
            return $"Use at most {MaximumNameLength} Unicode characters for a layer name.";
        }
        if (value.Length > 0 && string.IsNullOrWhiteSpace(value))
        {
            return "A layer name cannot contain only spaces. Clear the field to remove it.";
        }
        if (value.Any(char.IsControl))
        {
            return "Layer names cannot contain control characters or line breaks.";
        }
        try
        {
            XmlConvert.VerifyXmlChars(value);
        }
        catch (XmlException)
        {
            return "The layer name contains a character that XML cannot store.";
        }

        return null;
    }

    private static bool IsCurrentAttribute(
        string source,
        SvgAttributeSpan attribute) =>
        attribute.NameSpan.Start >= 0
        && attribute.NameSpan.End <= source.Length
        && attribute.ValueSpan.Start >= 0
        && attribute.ValueSpan.End <= source.Length
        && source.AsSpan(
                attribute.NameSpan.Start,
                attribute.NameSpan.Length)
            .SequenceEqual(attribute.QualifiedName)
        && source.AsSpan(
                attribute.ValueSpan.Start,
                attribute.ValueSpan.Length)
            .SequenceEqual(attribute.RawValue);

    private static int FindAttributeInsertionOffset(
        string source,
        SourceSpan startTag)
    {
        int insertion = startTag.End - 1;
        if (insertion < startTag.Start
            || insertion >= source.Length
            || source[insertion] != '>')
        {
            throw new InvalidOperationException(
                "The selected start tag is no longer current.");
        }
        if (insertion > startTag.Start && source[insertion - 1] == '/')
        {
            insertion--;
        }
        return insertion;
    }

    private static string EscapeAttributeValue(string value, char quote)
    {
        string escaped = value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal);
        return quote == '"'
            ? escaped.Replace("\"", "&quot;", StringComparison.Ordinal)
            : escaped.Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
