using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgDocumentIndexService
{
    private readonly SvgValidationService _validationService;

    public SvgDocumentIndexService()
        : this(new SvgValidationService())
    {
    }

    public SvgDocumentIndexService(SvgValidationService validationService)
    {
        _validationService = validationService
            ?? throw new ArgumentNullException(nameof(validationService));
    }

    public SvgDocumentIndexResult Build(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        SvgValidationResult validation = _validationService.Validate(source);
        if (!validation.IsValid)
        {
            return new SvgDocumentIndexResult(validation, null, validation.Message);
        }

        try
        {
            return new SvgDocumentIndexResult(
                validation,
                BuildValidatedSource(source),
                null);
        }
        catch (InvalidOperationException exception)
        {
            return new SvgDocumentIndexResult(
                validation,
                null,
                $"The valid SVG could not be indexed: {exception.Message}");
        }
    }

    private static SvgDocumentIndex BuildValidatedSource(string source)
    {
        List<ElementBuilder> roots = [];
        List<ElementBuilder> allElements = [];
        Stack<ElementBuilder> openElements = new();

        int offset = 0;
        while (offset < source.Length)
        {
            int markupStart = source.IndexOf('<', offset);
            if (markupStart < 0)
            {
                break;
            }

            if (StartsWith(source, markupStart, "<!--"))
            {
                offset = FindTerminator(source, markupStart + 4, "-->") + 3;
                continue;
            }

            if (StartsWith(source, markupStart, "<![CDATA["))
            {
                offset = FindTerminator(source, markupStart + 9, "]]>") + 3;
                continue;
            }

            if (StartsWith(source, markupStart, "<?"))
            {
                offset = FindTerminator(source, markupStart + 2, "?>") + 2;
                continue;
            }

            if (StartsWith(source, markupStart, "</"))
            {
                int close = source.IndexOf('>', markupStart + 2);
                if (close < 0 || openElements.Count == 0)
                {
                    throw new InvalidOperationException("An element end tag could not be mapped.");
                }

                ElementBuilder completed = openElements.Pop();
                completed.FullSpan = new SourceSpan(
                    completed.StartTagSpan.Start,
                    close + 1 - completed.StartTagSpan.Start);
                offset = close + 1;
                continue;
            }

            if (StartsWith(source, markupStart, "<!"))
            {
                offset = FindMarkupEnd(source, markupStart + 2) + 1;
                continue;
            }

            ParsedStartTag parsed = ParseStartTag(source, markupStart);
            ElementBuilder? parent = openElements.Count > 0 ? openElements.Peek() : null;
            int siblingIndex = parent?.Children.Count ?? roots.Count;
            string structuralPath = parent is null
                ? siblingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{parent.StructuralPath}/{siblingIndex}";
            ElementBuilder element = new(
                parsed.LocalName,
                parsed.QualifiedName,
                structuralPath,
                openElements.Count,
                parsed.Span,
                parsed.Attributes);

            if (parent is null)
            {
                roots.Add(element);
            }
            else
            {
                parent.Children.Add(element);
            }

            allElements.Add(element);
            if (parsed.IsEmpty)
            {
                element.FullSpan = parsed.Span;
            }
            else
            {
                openElements.Push(element);
            }

            offset = parsed.Span.End;
        }

        if (openElements.Count > 0)
        {
            throw new InvalidOperationException("One or more element spans were not closed.");
        }

        Dictionary<ElementBuilder, SvgElementNode> materialized = [];
        SvgElementNode Materialize(ElementBuilder builder)
        {
            if (materialized.TryGetValue(builder, out SvgElementNode? existing))
            {
                return existing;
            }

            SvgElementNode node = new(
                builder.Name,
                builder.QualifiedName,
                builder.StructuralPath,
                builder.Depth,
                builder.StartTagSpan,
                builder.FullSpan,
                builder.Attributes.AsReadOnly(),
                builder.Children.Select(Materialize).ToArray());
            materialized.Add(builder, node);
            return node;
        }

        SvgElementNode[] rootNodes = roots.Select(Materialize).ToArray();
        SvgElementNode[] flatNodes = allElements.Select(Materialize).ToArray();
        return new SvgDocumentIndex(rootNodes, flatNodes);
    }

    private static ParsedStartTag ParseStartTag(string source, int tagStart)
    {
        int cursor = tagStart + 1;
        int nameStart = cursor;
        while (cursor < source.Length && !IsNameTerminator(source[cursor]))
        {
            cursor++;
        }

        if (cursor == nameStart)
        {
            throw new InvalidOperationException("An element name could not be mapped.");
        }

        string qualifiedName = source[nameStart..cursor];
        string localName = GetLocalName(qualifiedName);
        List<SvgAttributeSpan> attributes = [];

        while (cursor < source.Length)
        {
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length)
            {
                break;
            }

            if (source[cursor] == '>')
            {
                return new ParsedStartTag(
                    localName,
                    qualifiedName,
                    new SourceSpan(tagStart, cursor + 1 - tagStart),
                    attributes,
                    IsEmpty: false);
            }

            if (source[cursor] == '/'
                && cursor + 1 < source.Length
                && source[cursor + 1] == '>')
            {
                return new ParsedStartTag(
                    localName,
                    qualifiedName,
                    new SourceSpan(tagStart, cursor + 2 - tagStart),
                    attributes,
                    IsEmpty: true);
            }

            int attributeNameStart = cursor;
            while (cursor < source.Length && !IsAttributeNameTerminator(source[cursor]))
            {
                cursor++;
            }

            if (cursor == attributeNameStart)
            {
                throw new InvalidOperationException("An attribute name could not be mapped.");
            }

            string attributeQualifiedName = source[attributeNameStart..cursor];
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] != '=')
            {
                throw new InvalidOperationException(
                    $"Attribute '{attributeQualifiedName}' has no mapped value.");
            }

            cursor++;
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] is not ('"' or '\''))
            {
                throw new InvalidOperationException(
                    $"Attribute '{attributeQualifiedName}' has no quoted value.");
            }

            char quote = source[cursor++];
            int valueStart = cursor;
            int valueEnd = source.IndexOf(quote, valueStart);
            if (valueEnd < 0)
            {
                throw new InvalidOperationException(
                    $"Attribute '{attributeQualifiedName}' has an unterminated value.");
            }

            attributes.Add(new SvgAttributeSpan(
                GetLocalName(attributeQualifiedName),
                attributeQualifiedName,
                new SourceSpan(
                    attributeNameStart,
                    attributeQualifiedName.Length),
                new SourceSpan(valueStart, valueEnd - valueStart),
                quote,
                source[valueStart..valueEnd]));
            cursor = valueEnd + 1;
        }

        throw new InvalidOperationException("An element start tag could not be mapped.");
    }

    private static int FindTerminator(
        string source,
        int searchStart,
        string terminator)
    {
        int result = source.IndexOf(terminator, searchStart, StringComparison.Ordinal);
        return result >= 0
            ? result
            : throw new InvalidOperationException($"XML section terminator '{terminator}' was not found.");
    }

    private static int FindMarkupEnd(string source, int searchStart)
    {
        char quote = '\0';
        for (int index = searchStart; index < source.Length; index++)
        {
            char current = source[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return index;
            }
        }

        throw new InvalidOperationException("An XML markup section was not terminated.");
    }

    private static bool StartsWith(string source, int offset, string value)
    {
        return source.AsSpan(offset).StartsWith(value, StringComparison.Ordinal);
    }

    private static void SkipWhitespace(string source, ref int offset)
    {
        while (offset < source.Length && char.IsWhiteSpace(source[offset]))
        {
            offset++;
        }
    }

    private static bool IsNameTerminator(char value) =>
        char.IsWhiteSpace(value) || value is '/' or '>';

    private static bool IsAttributeNameTerminator(char value) =>
        char.IsWhiteSpace(value) || value is '=' or '/' or '>';

    private static string GetLocalName(string qualifiedName)
    {
        int colon = qualifiedName.IndexOf(':');
        return colon >= 0 ? qualifiedName[(colon + 1)..] : qualifiedName;
    }

    private sealed class ElementBuilder
    {
        public ElementBuilder(
            string name,
            string qualifiedName,
            string structuralPath,
            int depth,
            SourceSpan startTagSpan,
            List<SvgAttributeSpan> attributes)
        {
            Name = name;
            QualifiedName = qualifiedName;
            StructuralPath = structuralPath;
            Depth = depth;
            StartTagSpan = startTagSpan;
            FullSpan = startTagSpan;
            Attributes = attributes;
        }

        public string Name { get; }

        public string QualifiedName { get; }

        public string StructuralPath { get; }

        public int Depth { get; }

        public SourceSpan StartTagSpan { get; }

        public SourceSpan FullSpan { get; set; }

        public List<SvgAttributeSpan> Attributes { get; }

        public List<ElementBuilder> Children { get; } = [];
    }

    private sealed record ParsedStartTag(
        string LocalName,
        string QualifiedName,
        SourceSpan Span,
        List<SvgAttributeSpan> Attributes,
        bool IsEmpty);
}
