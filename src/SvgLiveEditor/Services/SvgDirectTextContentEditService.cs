using System.Globalization;
using System.Xml;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgDirectTextContentEditService
{
    private readonly SvgDocumentIndexService _documentIndexService = new();

    public SvgDirectTextAnalysis Analyze(
        string source,
        SvgDocumentIndex documentIndex,
        SvgElementNode element)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(documentIndex);
        ArgumentNullException.ThrowIfNull(element);

        SvgElementNode? current = documentIndex.FindBestMatch(element.Identity);
        if (current is null
            || current.StartTagSpan != element.StartTagSpan
            || current.FullSpan != element.FullSpan)
        {
            return SvgDirectTextAnalysis.Rejected(
                "The text selection is stale; select it again.");
        }
        if (!SvgVisualTextIndexService.TryCreate(
                documentIndex,
                source,
                out SvgVisualTextIndexService? textIndex)
            || textIndex is null)
        {
            return SvgDirectTextAnalysis.Rejected(
                "The text source mapping is unavailable.");
        }

        return textIndex.AnalyzeDirectText(current);
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgDocumentIndex documentIndex,
        SvgElementNode element,
        string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        SvgDirectTextAnalysis analysis = Analyze(
            source,
            documentIndex,
            element);
        if (!analysis.IsEligible
            || analysis.Content is not SvgDirectTextContent content)
        {
            return SvgAttributeEditResult.Invalid(
                analysis.UnavailableReason
                ?? "The selected text cannot be edited safely.");
        }
        if (replacement.Length > SvgVisualTextIndexService.MaximumTextLength)
        {
            return SvgAttributeEditResult.Invalid(
                $"Text content is limited to {SvgVisualTextIndexService.MaximumTextLength.ToString(CultureInfo.InvariantCulture)} characters.");
        }
        if (!IsSafeXmlText(replacement))
        {
            return SvgAttributeEditResult.Invalid(
                "Text content contains an XML control character or an invalid Unicode sequence.");
        }
        if (replacement.Equals(content.Text, StringComparison.Ordinal))
        {
            return SvgAttributeEditResult.Success(edit: null);
        }

        string escaped = EscapeText(replacement);
        SourceTextEdit edit = new(
            content.ContentSpan.Start,
            content.ContentSpan.Length,
            escaped);
        string candidate = edit.Apply(source);
        SvgDocumentIndexResult validation = _documentIndexService.Build(candidate);
        return validation.Document is null
            ? SvgAttributeEditResult.Invalid(
                validation.Validation.Message
                ?? validation.IndexError
                ?? "The text edit did not produce a valid safe SVG.")
            : SvgAttributeEditResult.Success(edit);
    }

    private static bool IsSafeXmlText(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length
                    || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }
                index++;
                continue;
            }
            if (char.IsLowSurrogate(current)
                || char.IsControl(current)
                || !XmlConvert.IsXmlChar(current))
            {
                return false;
            }
        }

        return true;
    }

    private static string EscapeText(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
