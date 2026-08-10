using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgGroupService
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAuthoringAvailability GetGroupAvailability(
        string source,
        SvgDocumentIndex document,
        IReadOnlyList<SvgElementNode> selected,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        return TryValidateGroup(
            source,
            document,
            selected,
            isEffectivelyLocked,
            out _,
            out _,
            out string? error)
                ? new SvgAuthoringAvailability(true)
                : new SvgAuthoringAvailability(
                    false,
                    error ?? "The selected elements cannot be grouped safely.");
    }

    public SvgAuthoringEditResult CreateGroupEdit(
        string source,
        SvgDocumentIndex document,
        IReadOnlyList<SvgElementNode> selected,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        if (!TryValidateGroup(
                source,
                document,
                selected,
                isEffectivelyLocked,
                out _,
                out SvgElementNode[] ordered,
                out string? error))
        {
            return SvgAuthoringEditResult.Invalid(
                error ?? "The selected elements cannot be grouped safely.");
        }

        int start = ordered[0].FullSpan.Start;
        int end = ordered[^1].FullSpan.End;
        string candidate = source[..start]
            + "<g>"
            + source[start..end]
            + "</g>"
            + source[end..];
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        SvgElementNode? group = rebuilt.Document?.FindElementAtOffset(start + 1);
        if (!validation.IsValid
            || group is null
            || !group.Name.Equals("g", StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid(
                $"The group wrapper could not be validated: {validation.Message}");
        }

        return SvgAuthoringEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate),
            group.Identity);
    }

    public SvgAuthoringAvailability GetUngroupAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? group,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        string? error = ValidateUngroup(
            source,
            document,
            group,
            isEffectivelyLocked);
        return error is null
            ? new SvgAuthoringAvailability(true)
            : new SvgAuthoringAvailability(false, error);
    }

    public SvgAuthoringEditResult CreateUngroupEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? group,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        string? error = ValidateUngroup(
            source,
            document,
            group,
            isEffectivelyLocked);
        if (error is not null || group is null)
        {
            return SvgAuthoringEditResult.Invalid(
                error ?? "Select one neutral group to ungroup.");
        }

        int closingLength = group.QualifiedName.Length + 3;
        int closingStart = group.FullSpan.End - closingLength;
        if (closingStart < group.StartTagSpan.End
            || !source.AsSpan(closingStart, closingLength)
                .SequenceEqual($"</{group.QualifiedName}>"))
        {
            return SvgAuthoringEditResult.Invalid(
                "The group closing tag changed; select it again.");
        }

        string candidate = string.Concat(
            source.AsSpan(0, group.StartTagSpan.Start),
            source.AsSpan(
                group.StartTagSpan.End,
                closingStart - group.StartTagSpan.End),
            source.AsSpan(group.FullSpan.End));
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        int firstChildStart = group.Children[0].FullSpan.Start
            - group.StartTagSpan.Length;
        SvgElementNode? firstChild = rebuilt.Document?.FindElementAtOffset(
            Math.Min(firstChildStart + 1, candidate.Length - 1));
        if (!validation.IsValid || firstChild is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The ungrouped source could not be validated: {validation.Message}");
        }

        return SvgAuthoringEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate),
            firstChild.Identity);
    }

    private static bool TryValidateGroup(
        string source,
        SvgDocumentIndex document,
        IReadOnlyList<SvgElementNode> selected,
        Func<SvgElementNode, bool>? isEffectivelyLocked,
        out SvgElementNode? parent,
        out SvgElementNode[] ordered,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selected);
        parent = null;
        ordered = [];
        error = null;
        if (selected.Count < 2
            || selected.Count > SvgMultiSelectionService.MaximumSelectionCount
            || selected.Distinct().Count() != selected.Count)
        {
            error = "Select between 2 and 128 distinct sibling elements to group.";
            return false;
        }

        ordered = selected.OrderBy(item => item.FullSpan.Start).ToArray();
        parent = document.FindParent(ordered[0]);
        if (parent is null || parent.Name is not ("svg" or "g"))
        {
            error = "The selected elements do not have an eligible SVG/group parent.";
            return false;
        }
        SvgElementNode selectedParent = parent;
        if (ordered.Any(element =>
                !ReferenceEquals(document.FindParent(element), selectedParent)
                || !SvgLayerPolicy.IsLayerElement(element.Name)
                || SvgLayerPolicy.IsInsideDefinitionContainer(document, element)
                || !SvgSourceMutationUtilities.IsCurrentElement(source, element))
            || !SvgSourceMutationUtilities.IsCurrentElement(source, parent))
        {
            error = "Grouping requires current eligible elements under one parent.";
            return false;
        }
        if (ordered.Any(element =>
                isEffectivelyLocked?.Invoke(element) == true)
            || isEffectivelyLocked?.Invoke(parent) == true)
        {
            error = "Unlock every selected layer and its parent before grouping.";
            return false;
        }

        int first = IndexOfReference(parent.Children, ordered[0]);
        int last = IndexOfReference(parent.Children, ordered[^1]);
        if (first < 0
            || last - first + 1 != ordered.Length
            || !parent.Children.Skip(first).Take(ordered.Length)
                .SequenceEqual(ordered))
        {
            error = "Grouping a non-contiguous selection could change paint order and was rejected.";
            return false;
        }

        return true;
    }

    private static string? ValidateUngroup(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? group,
        Func<SvgElementNode, bool>? isEffectivelyLocked)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        if (group is null
            || !group.Name.Equals("g", StringComparison.Ordinal)
            || document.FindParent(group) is null
            || !SvgSourceMutationUtilities.IsCurrentElement(source, group))
        {
            return "Select one current group to ungroup.";
        }
        if (group.Children.Count == 0)
        {
            return "An empty group has no children to ungroup.";
        }
        if (isEffectivelyLocked?.Invoke(group) == true)
        {
            return "Unlock the group and its ancestors before ungrouping.";
        }
        if (group.Attributes.Count > 0)
        {
            SvgAttributeSpan attribute = group.Attributes[0];
            return $"Ungroup was rejected because removing '{attribute.QualifiedName}' could change rendering, references, or metadata.";
        }
        if (group.Children.Any(child => child.Name is
                "animate" or "animateMotion" or "animateTransform" or "set"))
        {
            return "Animated groups cannot be ungrouped safely.";
        }

        return null;
    }

    private static int IndexOfReference(
        IReadOnlyList<SvgElementNode> items,
        SvgElementNode target)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], target))
            {
                return index;
            }
        }
        return -1;
    }
}
