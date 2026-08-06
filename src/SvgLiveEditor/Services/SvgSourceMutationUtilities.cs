using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

internal static class SvgSourceMutationUtilities
{
    public static SvgElementNode? FindSvgRoot(SvgDocumentIndex document) =>
        document.Roots.FirstOrDefault(element =>
            element.Name.Equals("svg", StringComparison.Ordinal));

    public static bool IsCurrentElement(
        string source,
        SvgElementNode element)
    {
        if (element.FullSpan.Start < 0
            || element.FullSpan.Length <= 0
            || element.FullSpan.Start > source.Length - element.FullSpan.Length
            || element.StartTagSpan.Start != element.FullSpan.Start
            || element.StartTagSpan.Length < element.QualifiedName.Length + 2
            || element.StartTagSpan.End > element.FullSpan.End
            || element.StartTagSpan.End > source.Length
            || source[element.StartTagSpan.Start] != '<'
            || source[element.StartTagSpan.End - 1] != '>'
            || element.StartTagSpan.Start + 1
                > source.Length - element.QualifiedName.Length
            || !source.AsSpan(
                element.StartTagSpan.Start + 1,
                element.QualifiedName.Length).SequenceEqual(element.QualifiedName))
        {
            return false;
        }

        int nameEnd = element.StartTagSpan.Start
            + 1
            + element.QualifiedName.Length;
        if (nameEnd >= source.Length
            || source[nameEnd] is not (' ' or '\t' or '\r' or '\n' or '/' or '>'))
        {
            return false;
        }

        foreach (SvgAttributeSpan attribute in element.Attributes)
        {
            if (attribute.NameSpan.Start < element.StartTagSpan.Start
                || attribute.NameSpan.End > element.StartTagSpan.End
                || attribute.ValueSpan.Start <= element.StartTagSpan.Start
                || attribute.ValueSpan.End >= element.StartTagSpan.End
                || attribute.ValueSpan.Start > source.Length - attribute.ValueSpan.Length
                || attribute.NameSpan.Start > source.Length - attribute.NameSpan.Length
                || !source.AsSpan(
                    attribute.NameSpan.Start,
                    attribute.NameSpan.Length).SequenceEqual(attribute.QualifiedName)
                || !source.AsSpan(
                    attribute.ValueSpan.Start,
                    attribute.ValueSpan.Length).SequenceEqual(attribute.RawValue)
                || source[attribute.ValueSpan.Start - 1] != attribute.Quote
                || source[attribute.ValueSpan.End] != attribute.Quote)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsDescendantOrSelf(
        SvgElementNode ancestor,
        SvgElementNode candidate) =>
        candidate.FullSpan.Start >= ancestor.FullSpan.Start
        && candidate.FullSpan.End <= ancestor.FullSpan.End;

    public static IEnumerable<SvgElementNode> EnumerateSubtree(
        SvgElementNode root)
    {
        yield return root;
        foreach (SvgElementNode child in root.Children)
        {
            foreach (SvgElementNode descendant in EnumerateSubtree(child))
            {
                yield return descendant;
            }
        }
    }

    public static SourceTextEdit CreateMinimalEdit(
        string source,
        string candidate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);

        int prefix = 0;
        int commonLimit = Math.Min(source.Length, candidate.Length);
        while (prefix < commonLimit && source[prefix] == candidate[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < source.Length - prefix
            && suffix < candidate.Length - prefix
            && source[^(suffix + 1)] == candidate[^(suffix + 1)])
        {
            suffix++;
        }

        return new SourceTextEdit(
            prefix,
            source.Length - prefix - suffix,
            candidate.Substring(prefix, candidate.Length - prefix - suffix));
    }

    public static bool TryInsertFrontmostChild(
        string source,
        SvgElementNode parent,
        string fragment,
        out string candidate,
        out int insertedStart,
        out string? errorMessage)
    {
        candidate = source;
        insertedStart = -1;
        errorMessage = null;
        if (!IsCurrentElement(source, parent)
            || parent.Name is not ("svg" or "g"))
        {
            errorMessage = "The insertion parent is no longer a current SVG or group element.";
            return false;
        }

        string newline = DetectNewline(source);
        string parentIndent = GetLineIndent(source, parent.StartTagSpan.Start);
        string indentUnit = InferIndentUnit(source, parent, parentIndent);
        bool isSelfClosing = parent.StartTagSpan.Length >= 2
            && source.AsSpan(parent.StartTagSpan.End - 2, 2).SequenceEqual("/>");
        if (isSelfClosing)
        {
            int slash = parent.StartTagSpan.End - 2;
            bool useMultiline = newline.Length > 0
                && IsStandaloneOnLine(source, parent.FullSpan);
            string replacement = useMultiline
                ? string.Concat(
                    ">",
                    newline,
                    parentIndent,
                    indentUnit,
                    fragment,
                    newline,
                    parentIndent,
                    "</",
                    parent.QualifiedName,
                    ">")
                : $">{fragment}</{parent.QualifiedName}>";
            candidate = string.Concat(
                source.AsSpan(0, slash),
                replacement,
                source.AsSpan(parent.StartTagSpan.End));
            insertedStart = slash + 1
                + (useMultiline
                    ? newline.Length + parentIndent.Length + indentUnit.Length
                    : 0);
            return true;
        }

        int closingStart = FindClosingTagStart(source, parent);
        if (closingStart < parent.StartTagSpan.End)
        {
            errorMessage = "The insertion parent's closing tag is no longer current.";
            return false;
        }

        bool multiline = newline.Length > 0
            && source.AsSpan(parent.StartTagSpan.End, closingStart - parent.StartTagSpan.End)
                .IndexOfAny('\r', '\n') >= 0;
        string insertion = multiline
            ? string.Concat(indentUnit, fragment, newline, parentIndent)
            : fragment;
        candidate = source.Insert(closingStart, insertion);
        insertedStart = closingStart + (multiline ? indentUnit.Length : 0);
        return true;
    }

    public static string InsertAdjacentAfter(
        string source,
        SvgElementNode element,
        string fragment,
        out int insertedStart)
    {
        string newline = ReadFollowingNewline(source, element.FullSpan.End);
        string prefix = newline.Length == 0
            ? string.Empty
            : newline + GetLineIndent(source, element.FullSpan.Start);
        insertedStart = element.FullSpan.End + prefix.Length;
        return source.Insert(element.FullSpan.End, prefix + fragment);
    }

    public static string InsertAdjacentBefore(
        string source,
        SvgElementNode element,
        string fragment,
        out int insertedStart)
    {
        string newline = DetectNewline(source);
        string indent = GetLineIndent(source, element.FullSpan.Start);
        bool startsAfterIndent = element.FullSpan.Start >= indent.Length
            && source.AsSpan(
                element.FullSpan.Start - indent.Length,
                indent.Length).SequenceEqual(indent)
            && (element.FullSpan.Start == indent.Length
                || source[element.FullSpan.Start - indent.Length - 1]
                    is '\r' or '\n');
        string suffix = newline.Length > 0 && startsAfterIndent
            ? newline + indent
            : string.Empty;
        insertedStart = element.FullSpan.Start;
        return source.Insert(element.FullSpan.Start, fragment + suffix);
    }

    public static string DetectNewline(string source)
    {
        int crlf = source.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            return "\r\n";
        }

        return source.Contains('\n')
            ? "\n"
            : source.Contains('\r')
                ? "\r"
                : string.Empty;
    }

    private static int FindClosingTagStart(
        string source,
        SvgElementNode parent)
    {
        int searchStart = parent.StartTagSpan.End;
        int searchLength = parent.FullSpan.End - searchStart;
        int candidate = source.LastIndexOf(
            "</",
            parent.FullSpan.End - 1,
            searchLength,
            StringComparison.Ordinal);
        if (candidate < 0
            || candidate + 2 > source.Length - parent.QualifiedName.Length
            || !source.AsSpan(
                candidate + 2,
                parent.QualifiedName.Length).SequenceEqual(parent.QualifiedName))
        {
            return -1;
        }

        return candidate;
    }

    private static string InferIndentUnit(
        string source,
        SvgElementNode parent,
        string parentIndent)
    {
        SvgElementNode? firstChild = parent.Children.FirstOrDefault();
        if (firstChild is not null)
        {
            string childIndent = GetLineIndent(source, firstChild.FullSpan.Start);
            if (childIndent.Length > parentIndent.Length
                && childIndent.StartsWith(parentIndent, StringComparison.Ordinal))
            {
                return childIndent[parentIndent.Length..];
            }
        }

        return "  ";
    }

    private static string GetLineIndent(string source, int offset)
    {
        if (offset <= 0 || offset > source.Length)
        {
            return string.Empty;
        }

        int lineStart = offset - 1;
        while (lineStart >= 0 && source[lineStart] is not ('\r' or '\n'))
        {
            lineStart--;
        }
        lineStart++;

        int cursor = lineStart;
        while (cursor < offset && source[cursor] is ' ' or '\t')
        {
            cursor++;
        }

        return cursor == offset ? source[lineStart..offset] : string.Empty;
    }

    private static bool IsStandaloneOnLine(string source, SourceSpan span)
    {
        int lineStart = span.Start;
        while (lineStart > 0 && source[lineStart - 1] is not ('\r' or '\n'))
        {
            lineStart--;
        }
        if (source.AsSpan(lineStart, span.Start - lineStart).ContainsAnyExcept(' ', '\t'))
        {
            return false;
        }

        int lineEnd = span.End;
        while (lineEnd < source.Length && source[lineEnd] is not ('\r' or '\n'))
        {
            if (source[lineEnd] is not (' ' or '\t'))
            {
                return false;
            }
            lineEnd++;
        }

        return true;
    }

    private static string ReadFollowingNewline(string source, int offset)
    {
        if (offset < 0 || offset >= source.Length)
        {
            return string.Empty;
        }
        if (source.AsSpan(offset).StartsWith("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }
        return source[offset] is '\r' or '\n'
            ? source[offset].ToString()
            : string.Empty;
    }
}
