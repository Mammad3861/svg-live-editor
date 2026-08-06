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

    private readonly SvgValidationService _validationService = new();

    public SvgLayerOrderAvailability GetAvailability(
        SvgDocumentIndex document,
        SvgElementNode element,
        SvgLayerOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        SvgLayerPositionInfo positionInfo = GetPositionInfo(document, element);
        if (!positionInfo.IsEligible)
        {
            return Unavailable(
                positionInfo.UnavailableReason
                ?? "The selected element cannot be reordered.");
        }

        int currentIndex = positionInfo.Position - 1;
        int targetPosition = GetTargetPosition(
            currentIndex,
            positionInfo.Count,
            command);
        return targetPosition == currentIndex
            ? Unavailable(GetBoundaryReason(command, positionInfo.ParentLabel))
            : new SvgLayerOrderAvailability(true);
    }

    public SvgLayerPositionInfo GetPositionInfo(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        if (!SvgLayerPolicy.IsLayerElement(element.Name))
        {
            return UnavailablePosition(
                "Only visual artwork and groups have a layer position.");
        }

        SvgElementNode? parent = document.FindParent(element);
        if (parent is null || SvgLayerPolicy.IsInsideDefinitionContainer(document, parent))
        {
            return UnavailablePosition(
                "The selected element has no safe paint-order parent.");
        }

        IReadOnlyList<SvgElementNode> eligible = parent.Children
            .Where(IsEligibleSibling)
            .ToArray();
        int position = IndexOfReference(eligible, element);
        if (position < 0)
        {
            return UnavailablePosition(
                "The selected element is not an eligible paint-order sibling.");
        }

        return new SvgLayerPositionInfo(
            true,
            position + 1,
            eligible.Count,
            parent.DisplayLabel,
            "Arrange stays within this parent and cannot cross group or container boundaries.");
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

        SvgElementNode parent = document.FindParent(element)!;
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

    public SvgLayerMoveEditResult CreateMoveEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode element,
        SvgElementNode target,
        SvgLayerDropPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(target);

        if (placement is not SvgLayerDropPlacement.Before
            and not SvgLayerDropPlacement.After)
        {
            return SvgLayerMoveEditResult.Invalid(
                "Dropping inside a group requires the guarded reparent operation.");
        }
        if (ReferenceEquals(element, target))
        {
            return SvgLayerMoveEditResult.Invalid(
                "Drop before or after a different sibling layer.");
        }
        SvgElementNode? parent = document.FindParent(element);
        SvgElementNode? targetParent = document.FindParent(target);
        if (parent is null
            || targetParent is null
            || !ReferenceEquals(parent, targetParent))
        {
            return SvgLayerMoveEditResult.Invalid(
                "Layers can be reordered only within the same parent. Use the explicit guarded reparent operation to move a layer between parents.");
        }
        if (!IsEligibleSibling(element)
            || !IsEligibleSibling(target)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, parent))
        {
            return SvgLayerMoveEditResult.Invalid(
                "The selected source or target is not an eligible paint-order layer.");
        }

        SvgElementNode[] eligible = parent.Children
            .Where(IsEligibleSibling)
            .ToArray();
        int elementIndex = IndexOfReference(eligible, element);
        int targetIndex = IndexOfReference(eligible, target);
        bool wantsAfterTargetInSource =
            placement == SvgLayerDropPlacement.Before;
        bool isNoOp = wantsAfterTargetInSource
            ? elementIndex == targetIndex + 1
            : elementIndex + 1 == targetIndex;
        if (isNoOp)
        {
            return SvgLayerMoveEditResult.Invalid(
                "The layer is already at that paint-order position.");
        }
        if (!AreCurrentOrderedSpans(source, parent.Children)
            || !IsCurrentElement(source, element)
            || !IsCurrentElement(source, target))
        {
            return SvgLayerMoveEditResult.Invalid(
                "The source changed; select the layer again.");
        }

        SourceTextEdit edit = CreateRelativeMoveEdit(
            source,
            element,
            target,
            wantsAfterTargetInSource);
        string candidate = edit.Apply(source);
        SvgValidationResult validation = _validationService.Validate(candidate);
        if (!validation.IsValid)
        {
            return SvgLayerMoveEditResult.Invalid(
                $"The reorder would make the SVG invalid: {validation.Message}");
        }

        int elementChildIndex = IndexOfReference(parent.Children, element);
        int targetChildIndex = IndexOfReference(parent.Children, target);
        int preferredChildIndex = wantsAfterTargetInSource
            ? elementChildIndex < targetChildIndex
                ? targetChildIndex
                : targetChildIndex + 1
            : elementChildIndex < targetChildIndex
                ? targetChildIndex - 1
                : targetChildIndex;
        SvgElementIdentity preferredSelection = new(
            element.Name,
            element.Id,
            $"{parent.StructuralPath}/{preferredChildIndex}");
        return SvgLayerMoveEditResult.Success(edit, preferredSelection);
    }

    public static bool IsPaintableElement(string elementName) =>
        PaintableElementNames.Contains(elementName);

    public static bool IsOrderableLayer(string elementName) =>
        SvgLayerPolicy.IsLayerElement(elementName);

    private static SvgLayerOrderAvailability Unavailable(string reason) =>
        new(false, reason);

    private static SvgLayerPositionInfo UnavailablePosition(string reason) =>
        new(false, 0, 0, string.Empty, string.Empty, reason);

    private static string GetBoundaryReason(
        SvgLayerOrderCommand command,
        string parentLabel)
    {
        string edge = command is SvgLayerOrderCommand.BringForward
            or SvgLayerOrderCommand.BringToFront
            ? "frontmost"
            : "backmost";
        return $"The selected element is already the {edge} eligible child of {parentLabel}. Arrange cannot cross parent or group boundaries.";
    }

    private static bool IsEligibleSibling(SvgElementNode element) =>
        SvgLayerPolicy.IsLayerElement(element.Name);

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

    private static SourceTextEdit CreateRelativeMoveEdit(
        string source,
        SvgElementNode element,
        SvgElementNode target,
        bool afterTarget)
    {
        bool movingForward = element.FullSpan.Start < target.FullSpan.Start;
        string selectedText = source.Substring(
            element.FullSpan.Start,
            element.FullSpan.Length);
        if (movingForward && afterTarget)
        {
            return new SourceTextEdit(
                element.FullSpan.Start,
                target.FullSpan.End - element.FullSpan.Start,
                string.Concat(
                    source.AsSpan(
                        element.FullSpan.End,
                        target.FullSpan.End - element.FullSpan.End),
                    selectedText));
        }
        if (movingForward)
        {
            return new SourceTextEdit(
                element.FullSpan.Start,
                target.FullSpan.Start - element.FullSpan.Start,
                string.Concat(
                    source.AsSpan(
                        element.FullSpan.End,
                        target.FullSpan.Start - element.FullSpan.End),
                    selectedText));
        }
        if (afterTarget)
        {
            return new SourceTextEdit(
                target.FullSpan.End,
                element.FullSpan.End - target.FullSpan.End,
                string.Concat(
                    selectedText,
                    source.AsSpan(
                        target.FullSpan.End,
                        element.FullSpan.Start - target.FullSpan.End)));
        }

        return new SourceTextEdit(
            target.FullSpan.Start,
            element.FullSpan.End - target.FullSpan.Start,
            string.Concat(
                selectedText,
                source.AsSpan(
                    target.FullSpan.Start,
                    element.FullSpan.Start - target.FullSpan.Start)));
    }
}
