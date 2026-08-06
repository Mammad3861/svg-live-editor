using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayerVisibilityService
{
    private readonly SvgValidationService _validationService = new();

    public SvgLayerVisibilityState Analyze(
        SvgDocumentIndex document,
        SvgElementNode element,
        bool ownsHiddenAttribute)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        SvgAttributeSpan? display = element.FindAttribute("display");
        SvgAttributeSpan? visibility = element.FindAttribute("visibility");
        SvgAttributeSpan? style = element.FindAttribute("style");
        bool directlyHidden = IsNone(display)
            || IsHiddenVisibility(visibility)
            || StyleHides(style);
        bool hiddenByAncestor = false;
        for (SvgElementNode? ancestor = document.FindParent(element);
             ancestor is not null;
             ancestor = document.FindParent(ancestor))
        {
            if (IsNone(ancestor.FindAttribute("display"))
                || IsHiddenVisibility(ancestor.FindAttribute("visibility"))
                || StyleHides(ancestor.FindAttribute("style")))
            {
                hiddenByAncestor = true;
                break;
            }
        }

        string? unavailableReason = GetUnavailableReason(
            element,
            display,
            visibility,
            style,
            ownsHiddenAttribute,
            hiddenByAncestor);
        return new SvgLayerVisibilityState(
            !directlyHidden && !hiddenByAncestor,
            directlyHidden,
            hiddenByAncestor,
            unavailableReason is null,
            unavailableReason);
    }

    public SvgLayerVisibilityEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode element,
        bool ownsHiddenAttribute)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        SvgLayerVisibilityState state = Analyze(
            document,
            element,
            ownsHiddenAttribute);
        if (!state.CanToggle)
        {
            return SvgLayerVisibilityEditResult.Invalid(
                state.UnavailableReason
                ?? "Visibility cannot be changed safely for this layer.");
        }

        SourceTextEdit? edit = state.IsDirectlyHidden
            ? CreateRemoveDisplayEdit(source, element)
            : CreateAddDisplayEdit(source, element);
        if (edit is null)
        {
            return SvgLayerVisibilityEditResult.Invalid(
                "The source changed; select the layer again.");
        }

        string candidate = edit.Apply(source);
        SvgValidationResult validation = _validationService.Validate(candidate);
        return validation.IsValid
            ? SvgLayerVisibilityEditResult.Success(
                edit,
                ownsHiddenAttributeAfterEdit: !state.IsDirectlyHidden)
            : SvgLayerVisibilityEditResult.Invalid(
                $"The visibility change would make the SVG invalid: {validation.Message}");
    }

    private static string? GetUnavailableReason(
        SvgElementNode element,
        SvgAttributeSpan? display,
        SvgAttributeSpan? visibility,
        SvgAttributeSpan? style,
        bool ownsHiddenAttribute,
        bool hiddenByAncestor)
    {
        if (!SvgLayerPolicy.IsLayerElement(element.Name))
        {
            return "This XML element is not a visual layer.";
        }
        if (style is not null)
        {
            return "Visibility is owned by an inline style and is not overwritten.";
        }
        if (visibility is not null)
        {
            return "The authored visibility attribute is preserved; edit it in Source.";
        }
        if (HasVisibilityAnimation(element))
        {
            return "Animated visibility or display is not changed by Layers.";
        }
        if (display is not null
            && !(ownsHiddenAttribute && IsNone(display)))
        {
            return "The authored display attribute is preserved; edit it in Source.";
        }
        if (hiddenByAncestor)
        {
            return "This layer is hidden by a parent group; toggle the parent instead.";
        }

        return null;
    }

    private static bool HasVisibilityAnimation(SvgElementNode element) =>
        element.Children.Any(child =>
            child.Name is "animate" or "set" or "animateTransform"
            && (child.FindAttribute("attributeName")?.RawValue is
                "display" or "visibility"));

    private static bool IsNone(SvgAttributeSpan? attribute) =>
        TryDecode(attribute, out string value)
        && value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    private static bool IsHiddenVisibility(SvgAttributeSpan? attribute) =>
        TryDecode(attribute, out string value)
        && value.Trim() is "hidden" or "collapse";

    private static bool StyleHides(SvgAttributeSpan? style)
    {
        if (!TryDecode(style, out string value))
        {
            return style is not null;
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(declaration => declaration.Split(':', 2))
            .Any(parts => parts.Length == 2
                && (parts[0].Trim().Equals(
                        "display",
                        StringComparison.OrdinalIgnoreCase)
                    && parts[1].Trim().Equals(
                        "none",
                        StringComparison.OrdinalIgnoreCase)
                    || parts[0].Trim().Equals(
                        "visibility",
                        StringComparison.OrdinalIgnoreCase)
                    && parts[1].Trim() is "hidden" or "collapse"));
    }

    private static bool TryDecode(
        SvgAttributeSpan? attribute,
        out string value)
    {
        value = string.Empty;
        return attribute is not null
            && SvgXmlAttributeValueDecoder.TryDecode(
                attribute.RawValue,
                out value);
    }

    private static SourceTextEdit? CreateAddDisplayEdit(
        string source,
        SvgElementNode element)
    {
        if (!IsCurrentElement(source, element))
        {
            return null;
        }

        int insertionOffset = element.StartTagSpan.End - 1;
        if (insertionOffset < element.StartTagSpan.Start
            || insertionOffset >= source.Length
            || source[insertionOffset] != '>')
        {
            return null;
        }
        if (insertionOffset > element.StartTagSpan.Start
            && source[insertionOffset - 1] == '/')
        {
            insertionOffset--;
        }

        bool needsSpace = insertionOffset == 0
            || !char.IsWhiteSpace(source[insertionOffset - 1]);
        return new SourceTextEdit(
            insertionOffset,
            0,
            $"{(needsSpace ? " " : string.Empty)}display=\"none\"");
    }

    private static SourceTextEdit? CreateRemoveDisplayEdit(
        string source,
        SvgElementNode element)
    {
        SvgAttributeSpan? display = element.FindAttribute("display");
        if (display is null
            || !IsNone(display)
            || !IsCurrentElement(source, element)
            || display.NameSpan.Start < 0
            || display.ValueSpan.Start < 0
            || display.ValueSpan.End >= source.Length
            || source[display.ValueSpan.End] != display.Quote
            || !source.AsSpan(
                    display.NameSpan.Start,
                    display.NameSpan.Length).SequenceEqual(display.QualifiedName)
            || !source.AsSpan(
                    display.ValueSpan.Start,
                    display.ValueSpan.Length).SequenceEqual(display.RawValue))
        {
            return null;
        }

        int start = display.NameSpan.Start;
        if (start > element.StartTagSpan.Start
            && source[start - 1] is ' ' or '\t')
        {
            start--;
        }
        int end = display.ValueSpan.End + 1;
        return new SourceTextEdit(start, end - start, string.Empty);
    }

    private static bool IsCurrentElement(
        string source,
        SvgElementNode element) =>
        element.StartTagSpan.Start >= 0
        && element.StartTagSpan.Length > 0
        && element.StartTagSpan.Start
            <= source.Length - element.StartTagSpan.Length
        && element.StartTagSpan.Start + 1
            <= source.Length - element.QualifiedName.Length
        && source[element.StartTagSpan.Start] == '<'
        && source.AsSpan(
            element.StartTagSpan.Start + 1,
            element.QualifiedName.Length).SequenceEqual(element.QualifiedName);
}
