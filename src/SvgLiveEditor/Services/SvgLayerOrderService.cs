using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayerOrderService
{
    private static readonly HashSet<string> PaintableElementNames =
        new(StringComparer.Ordinal)
        {
            "rect",
            "circle",
            "ellipse",
            "line",
            "text",
            "path",
            "polygon",
            "polyline"
        };

    private static readonly HashSet<string> DefinitionContainerNames =
        new(StringComparer.Ordinal)
        {
            "defs",
            "clipPath",
            "filter",
            "linearGradient",
            "marker",
            "mask",
            "metadata",
            "pattern",
            "radialGradient",
            "symbol"
        };

    private readonly SvgValidationService _validationService = new();

    public SvgLayerOrderAvailability GetAvailability(
        SvgDocumentIndex document,
        SvgElementNode element,
        SvgLayerOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        if (!PaintableElementNames.Contains(element.Name))
        {
            return Unavailable("Only supported paintable elements can be reordered.");
        }

        SvgElementNode? parent = FindParent(document, element);
        if (parent is null || IsInsideDefinitionContainer(document, parent))
        {
            return Unavailable("The selected element has no safe paint-order parent.");
        }

        IReadOnlyList<SvgElementNode> eligible = parent.Children
            .Where(IsEligibleSibling)
            .ToArray();
        int position = IndexOfReference(eligible, element);
        if (position < 0)
        {
            return Unavailable("The selected element is not an eligible paint-order sibling.");
        }

        int targetPosition = GetTargetPosition(position, eligible.Count, command);
        return targetPosition == position
            ? Unavailable("The selected element is already at that boundary.")
            : new SvgLayerOrderAvailability(true);
    }

    public SvgLayerOrderEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode element,
        SvgLayerOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        SvgLayerOrderAvailability availability =
            GetAvailability(document, element, command);
        if (!availability.CanExecute)
        {
            return SvgLayerOrderEditResult.Invalid(
                availability.UnavailableReason ?? "The element cannot be reordered.");
        }

        SvgElementNode parent = FindParent(document, element)!;
        SvgElementNode[] eligible = parent.Children
            .Where(IsEligibleSibling)
            .ToArray();
        int position = IndexOfReference(eligible, element);
        int targetPosition = GetTargetPosition(position, eligible.Length, command);
        SvgElementNode target = eligible[targetPosition];

        if (!AreCurrentOrderedSpans(source, parent.Children)
            || !IsCurrentElement(source, element)
            || !IsCurrentElement(source, target))
        {
            return SvgLayerOrderEditResult.Invalid(
                "The source changed; select the element again.");
        }

        bool movingForward = element.FullSpan.Start < target.FullSpan.Start;
        int editStart = movingForward
            ? element.FullSpan.Start
            : target.FullSpan.Start;
        int editEnd = movingForward
            ? target.FullSpan.End
            : element.FullSpan.End;
        string selectedText = source.Substring(
            element.FullSpan.Start,
            element.FullSpan.Length);
        string replacement = movingForward
            ? string.Concat(
                source.AsSpan(element.FullSpan.End, target.FullSpan.End - element.FullSpan.End),
                selectedText)
            : string.Concat(
                selectedText,
                source.AsSpan(target.FullSpan.Start, element.FullSpan.Start - target.FullSpan.Start));
        SourceTextEdit edit = new(editStart, editEnd - editStart, replacement);
        string candidate = edit.Apply(source);
        SvgValidationResult validation = _validationService.Validate(candidate);
        if (!validation.IsValid)
        {
            return SvgLayerOrderEditResult.Invalid(
                $"The reorder would make the SVG invalid: {validation.Message}");
        }

        int targetChildIndex = IndexOfReference(parent.Children, target);
        SvgElementIdentity preferredSelection = new(
            element.Name,
            element.Id,
            $"{parent.StructuralPath}/{targetChildIndex}");
        return SvgLayerOrderEditResult.Success(edit, preferredSelection);
    }

    public static bool IsPaintableElement(string elementName) =>
        PaintableElementNames.Contains(elementName);

    private static SvgLayerOrderAvailability Unavailable(string reason) =>
        new(false, reason);

    private static bool IsEligibleSibling(SvgElementNode element) =>
        PaintableElementNames.Contains(element.Name)
        && !DefinitionContainerNames.Contains(element.Name);

    private static int GetTargetPosition(
        int position,
        int count,
        SvgLayerOrderCommand command) =>
        command switch
        {
            SvgLayerOrderCommand.BringToFront => count - 1,
            SvgLayerOrderCommand.BringForward => Math.Min(count - 1, position + 1),
            SvgLayerOrderCommand.SendBackward => Math.Max(0, position - 1),
            SvgLayerOrderCommand.SendToBack => 0,
            _ => position
        };

    private static SvgElementNode? FindParent(
        SvgDocumentIndex document,
        SvgElementNode element) =>
        document.Elements.FirstOrDefault(candidate =>
            candidate.Children.Any(child => ReferenceEquals(child, element)));

    private static bool IsInsideDefinitionContainer(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        SvgElementNode? current = element;
        while (current is not null)
        {
            if (DefinitionContainerNames.Contains(current.Name))
            {
                return true;
            }

            current = FindParent(document, current);
        }

        return false;
    }

    private static bool AreCurrentOrderedSpans(
        string source,
        IReadOnlyList<SvgElementNode> children)
    {
        int previousEnd = -1;
        foreach (SvgElementNode child in children)
        {
            if (!IsCurrentElement(source, child)
                || child.FullSpan.Start < previousEnd)
            {
                return false;
            }

            previousEnd = child.FullSpan.End;
        }

        return true;
    }

    private static bool IsCurrentElement(string source, SvgElementNode element) =>
        element.FullSpan.Start >= 0
        && element.FullSpan.Length > 0
        && element.FullSpan.Start <= source.Length - element.FullSpan.Length
        && element.StartTagSpan.Start == element.FullSpan.Start
        && element.StartTagSpan.Start + 1 <= source.Length - element.QualifiedName.Length
        && source[element.StartTagSpan.Start] == '<'
        && source.AsSpan(
            element.StartTagSpan.Start + 1,
            element.QualifiedName.Length).SequenceEqual(element.QualifiedName);

    private static int IndexOfReference(
        IReadOnlyList<SvgElementNode> elements,
        SvgElementNode target)
    {
        for (int index = 0; index < elements.Count; index++)
        {
            if (ReferenceEquals(elements[index], target))
            {
                return index;
            }
        }

        return -1;
    }
}
