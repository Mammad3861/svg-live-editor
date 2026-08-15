using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgSourceNavigationSpanService
{
    public SourceSpan GetPreferredSpan(
        string source,
        SvgElementNode element,
        InspectorSelectionOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(element);

        return origin == InspectorSelectionOrigin.PreviewNavigation
            && TryGetDirectTextContentSpan(source, element, out SourceSpan span)
                ? span
                : element.StartTagSpan;
    }

    public bool TryGetDirectTextContentSpan(
        string source,
        SvgElementNode element,
        out SourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(element);
        span = default;
        if (!element.Name.Equals("text", StringComparison.Ordinal)
            || element.Children.Count != 0
            || !IsCurrentSpan(source, element.StartTagSpan)
            || !IsCurrentSpan(source, element.FullSpan)
            || element.StartTagSpan.Length < 2
            || element.StartTagSpan.Start != element.FullSpan.Start
            || element.StartTagSpan.End >= element.FullSpan.End
            || source.AsSpan(
                    element.StartTagSpan.End - 2,
                    2).SequenceEqual("/>"))
        {
            return false;
        }

        string closingTag = $"</{element.QualifiedName}>";
        int closingStart = element.FullSpan.End - closingTag.Length;
        if (closingStart < element.StartTagSpan.End
            || !source.AsSpan(closingStart, closingTag.Length)
                .SequenceEqual(closingTag))
        {
            return false;
        }

        int contentLength = closingStart - element.StartTagSpan.End;
        ReadOnlySpan<char> content = source.AsSpan(
            element.StartTagSpan.End,
            contentLength);
        if (content.Contains('<'))
        {
            // Nested markup, CDATA, comments, and other non-plain content keep
            // the conservative start-tag navigation behavior.
            return false;
        }

        span = new SourceSpan(element.StartTagSpan.End, contentLength);
        return true;
    }

    private static bool IsCurrentSpan(string source, SourceSpan span) =>
        span.Start >= 0
        && span.Length > 0
        && span.Start <= source.Length - span.Length;
}
