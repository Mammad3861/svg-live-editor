using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgAttributeEditService
{
    private readonly SvgPropertyValueValidator _valueValidator;
    private readonly SvgValidationService _svgValidationService;

    public SvgAttributeEditService()
        : this(new SvgPropertyValueValidator(), new SvgValidationService())
    {
    }

    public SvgAttributeEditService(
        SvgPropertyValueValidator valueValidator,
        SvgValidationService svgValidationService)
    {
        _valueValidator = valueValidator
            ?? throw new ArgumentNullException(nameof(valueValidator));
        _svgValidationService = svgValidationService
            ?? throw new ArgumentNullException(nameof(svgValidationService));
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgElementNode element,
        string attributeName,
        string value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentNullException.ThrowIfNull(value);

        string? valueError = _valueValidator.Validate(
            element.Name,
            attributeName,
            value);
        if (valueError is not null)
        {
            return SvgAttributeEditResult.Invalid(valueError);
        }
        SvgPropertyDefinition definition =
            SvgPropertySchema.Find(element.Name, attributeName)!;

        if (!IsCurrentSpan(source, element.StartTagSpan)
            || element.StartTagSpan.Start + 1 > source.Length - element.QualifiedName.Length
            || source[element.StartTagSpan.Start] != '<'
            || !source.AsSpan(
                    element.StartTagSpan.Start + 1,
                    element.QualifiedName.Length).SequenceEqual(element.QualifiedName))
        {
            return SvgAttributeEditResult.Invalid(
                "The source changed; select the element again.");
        }

        SvgAttributeSpan? existing = element.FindAttribute(attributeName);
        if (existing is null
            && value.Length == 0
            && definition.RemoveWhenEmpty)
        {
            return SvgAttributeEditResult.Success(edit: null);
        }

        SourceTextEdit edit;
        if (existing is not null)
        {
            if (!IsCurrentSpan(source, existing.NameSpan)
                || !IsCurrentSpan(source, existing.ValueSpan)
                || !source.AsSpan(
                        existing.NameSpan.Start,
                        existing.NameSpan.Length).SequenceEqual(existing.QualifiedName)
                || !source.AsSpan(
                        existing.ValueSpan.Start,
                        existing.ValueSpan.Length).SequenceEqual(existing.RawValue))
            {
                return SvgAttributeEditResult.Invalid(
                    "The source changed; select the element again.");
            }

            if (value.Length == 0 && definition.RemoveWhenEmpty)
            {
                int attributeEnd = existing.ValueSpan.End + 1;
                if (attributeEnd > source.Length
                    || source[attributeEnd - 1] != existing.Quote)
                {
                    return SvgAttributeEditResult.Invalid(
                        "The source changed; select the element again.");
                }

                int attributeStart = existing.NameSpan.Start;
                if (attributeStart > element.StartTagSpan.Start
                    && source[attributeStart - 1] is ' ' or '\t')
                {
                    attributeStart--;
                }

                edit = new SourceTextEdit(
                    attributeStart,
                    attributeEnd - attributeStart,
                    string.Empty);
            }
            else
            {
                string escapedValue = EscapeAttributeValue(value, existing.Quote);
                if (source.AsSpan(
                        existing.ValueSpan.Start,
                        existing.ValueSpan.Length).SequenceEqual(escapedValue))
                {
                    return SvgAttributeEditResult.Success(edit: null);
                }

                edit = new SourceTextEdit(
                    existing.ValueSpan.Start,
                    existing.ValueSpan.Length,
                    escapedValue);
            }
        }
        else
        {
            int insertionOffset;
            try
            {
                insertionOffset = FindAttributeInsertionOffset(
                    source,
                    element.StartTagSpan);
            }
            catch (InvalidOperationException)
            {
                return SvgAttributeEditResult.Invalid(
                    "The source changed; select the element again.");
            }

            bool needsLeadingSpace = insertionOffset <= 0
                || !char.IsWhiteSpace(source[insertionOffset - 1]);
            string escapedValue = EscapeAttributeValue(value, '"');
            string insertion = $"{(needsLeadingSpace ? " " : string.Empty)}{attributeName}=\"{escapedValue}\"";
            edit = new SourceTextEdit(insertionOffset, 0, insertion);
        }

        string candidate = edit.Apply(source);
        SvgValidationResult validation = _svgValidationService.Validate(candidate);
        return validation.IsValid
            ? SvgAttributeEditResult.Success(edit)
            : SvgAttributeEditResult.Invalid(
                $"The change would make the SVG invalid: {validation.Message}");
    }

    private static bool IsCurrentSpan(string source, SourceSpan span)
    {
        return span.Start >= 0
            && span.Length >= 0
            && span.Start <= source.Length - span.Length;
    }

    private static int FindAttributeInsertionOffset(
        string source,
        SourceSpan startTagSpan)
    {
        int closingBracket = startTagSpan.End - 1;
        if (closingBracket < startTagSpan.Start
            || closingBracket >= source.Length
            || source[closingBracket] != '>')
        {
            throw new InvalidOperationException("The selected start tag is no longer current.");
        }

        int insertionOffset = closingBracket;
        if (insertionOffset > startTagSpan.Start
            && source[insertionOffset - 1] == '/')
        {
            insertionOffset--;
        }

        return insertionOffset;
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
