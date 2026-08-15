using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgMultiVisualMoveService
{
    private readonly SvgVisualMoveService _moveService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        double deltaX,
        double deltaY,
        Func<SvgElementNode, bool>? isEffectivelyLocked,
        Func<SvgElementNode, bool>? isEffectivelyVisible)
    {
        string? error = ValidateSelection(
            document,
            elements,
            isEffectivelyLocked,
            isEffectivelyVisible);
        return error is null
            ? CreateEdit(source, elements, deltaX, deltaY)
            : SvgAttributeEditResult.Invalid(error);
    }

    public SvgAuthoringAvailability GetAvailability(
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null,
        Func<SvgElementNode, bool>? isEffectivelyVisible = null)
    {
        string? error = ValidateSelection(
            document,
            elements,
            isEffectivelyLocked,
            isEffectivelyVisible);
        return error is null
            ? new SvgAuthoringAvailability(true)
            : new SvgAuthoringAvailability(false, error);
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        IReadOnlyList<SvgVisualElement> elements,
        double deltaX,
        double deltaY)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return CreateEdit(
            source,
            elements.Select(element =>
                new SvgVisualMoveDelta(element, deltaX, deltaY)).ToArray());
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        IReadOnlyList<SvgVisualMoveDelta> movements)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(movements);
        if (movements.Count == 0
            || movements.Count > SvgMultiSelectionService.MaximumSelectionCount)
        {
            return SvgAttributeEditResult.Invalid(
                "Select between 1 and 128 movable elements.");
        }
        if (movements.Select(item => item.Element.SourceElement.Identity)
                .Distinct()
                .Count() != movements.Count)
        {
            return SvgAttributeEditResult.Invalid(
                "The movement contains a duplicate element identity.");
        }

        List<SourceTextEdit> edits = [];
        foreach (SvgVisualMoveDelta movement in movements)
        {
            SvgAttributeEditResult result = _moveService.CreateEdit(
                source,
                movement.Element,
                movement.DeltaX,
                movement.DeltaY);
            if (!result.IsSuccess)
            {
                return SvgAttributeEditResult.Invalid(
                    result.ErrorMessage
                    ?? "Every selected element must be safely movable.");
            }
            if (result.Edit is not null)
            {
                edits.Add(result.Edit);
            }
        }

        if (edits.Count == 0)
        {
            return SvgAttributeEditResult.Success(edit: null);
        }
        SourceTextEdit[] ordered = edits
            .OrderByDescending(edit => edit.Start)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Start
                    < ordered[index].Start + ordered[index].Length)
            {
                return SvgAttributeEditResult.Invalid(
                    "Overlapping selected source spans cannot be moved safely.");
            }
        }

        string candidate = source;
        foreach (SourceTextEdit edit in ordered)
        {
            candidate = edit.Apply(candidate);
        }
        SvgValidationResult validation = _validationService.Validate(candidate);
        if (!validation.IsValid)
        {
            return SvgAttributeEditResult.Invalid(
                $"The combined movement would make the SVG invalid: {validation.Message}");
        }

        return SvgAttributeEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate));
    }

    private static string? ValidateSelection(
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        Func<SvgElementNode, bool>? isEffectivelyLocked,
        Func<SvgElementNode, bool>? isEffectivelyVisible)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0
            || elements.Count > SvgMultiSelectionService.MaximumSelectionCount
            || elements.Select(item => item.SourceElement.Identity)
                .Distinct().Count() != elements.Count
            || elements.Any(element =>
                !document.Elements.Contains(element.SourceElement)))
        {
            return "The selection changed before the move could be completed.";
        }
        if (elements.Any(element =>
                isEffectivelyLocked?.Invoke(element.SourceElement) == true))
        {
            return "The selection contains a locked element or locked ancestor.";
        }
        if (elements.Any(element =>
                isEffectivelyVisible?.Invoke(element.SourceElement) == false))
        {
            return "The selection contains a hidden element.";
        }
        if (elements.Any(element =>
                !element.IsMovable || element.Geometry is null))
        {
            return "The selection contains an unsupported or unmeasurable element.";
        }

        SvgElementNode? parent = document.FindParent(elements[0].SourceElement);
        if (parent is null
            || elements.Any(element => !ReferenceEquals(
                document.FindParent(element.SourceElement),
                parent)))
        {
            return "The selected elements must share a compatible parent.";
        }
        return null;
    }
}
