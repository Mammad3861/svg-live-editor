using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgMultiVisualMoveService
{
    private readonly SvgVisualMoveService _moveService = new();
    private readonly SvgValidationService _validationService = new();

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
}

