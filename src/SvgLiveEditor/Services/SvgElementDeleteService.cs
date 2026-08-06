using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgElementDeleteService
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLocalReferenceService _referenceService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAuthoringAvailability GetAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? element,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        if (element is null
            || element.Name.Equals("svg", StringComparison.Ordinal)
            || !SvgLayerPolicy.IsLayerElement(element.Name)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, element))
        {
            return new SvgAuthoringAvailability(
                false,
                "Select visual artwork or a non-root group to delete.");
        }
        if (!SvgSourceMutationUtilities.IsCurrentElement(source, element))
        {
            return new SvgAuthoringAvailability(
                false,
                "The source changed; select the element again.");
        }
        if (isEffectivelyLocked?.Invoke(element) == true)
        {
            return new SvgAuthoringAvailability(
                false,
                "Unlock the layer and its parent group before deleting it.");
        }

        return new SvgAuthoringAvailability(true);
    }

    public SvgAuthoringEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? element,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);

        SvgAuthoringAvailability availability = GetAvailability(
            source,
            document,
            element,
            isEffectivelyLocked);
        if (!availability.CanExecute || element is null)
        {
            return SvgAuthoringEditResult.Invalid(
                availability.UnavailableReason
                ?? "The element cannot be deleted.");
        }

        HashSet<SvgElementNode> subtree = SvgSourceMutationUtilities
            .EnumerateSubtree(element)
            .ToHashSet();
        HashSet<string> removedIds = subtree
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        SvgLocalReference? blockingReference;
        try
        {
            blockingReference = _referenceService
                .FindReferences(document.Elements.Where(item => !subtree.Contains(item)))
                .FirstOrDefault(reference => removedIds.Contains(reference.TargetId));
        }
        catch (InvalidOperationException exception)
        {
            return SvgAuthoringEditResult.Invalid(exception.Message);
        }
        if (blockingReference is not null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"Delete was blocked because {blockingReference.Element.DisplayLabel} refers to ID '{blockingReference.TargetId}'.");
        }

        SvgElementNode? parent = document.FindParent(element);
        SvgElementNode? preferredBeforeDelete = FindPreferredSelection(
            parent,
            element);
        string candidate = string.Concat(
            source.AsSpan(0, element.FullSpan.Start),
            source.AsSpan(element.FullSpan.End));
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        if (!validation.IsValid || rebuilt.Document is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"Delete would make the SVG invalid: {validation.Message}");
        }

        SvgElementNode? preferred = FindAfterDelete(
            rebuilt.Document,
            preferredBeforeDelete,
            element,
            parent);
        if (preferred is null)
        {
            return SvgAuthoringEditResult.Invalid(
                "A safe selection could not be restored after deletion.");
        }

        bool confirm = SvgLayerPolicy.IsGroup(element.Name)
            && HasMeaningfulContent(source, element);
        int descendantCount = CountDescendants(element);
        string? confirmation = !confirm
            ? null
            : descendantCount > 0
                ? $"Delete {element.DisplayLabel} and its {descendantCount} descendant element(s)?"
                : $"Delete {element.DisplayLabel} and all of its contents?";
        return SvgAuthoringEditResult.Success(
            new SourceTextEdit(element.FullSpan.Start, element.FullSpan.Length, string.Empty),
            preferred.Identity,
            confirm,
            confirmation);
    }

    private static SvgElementNode? FindPreferredSelection(
        SvgElementNode? parent,
        SvgElementNode element)
    {
        if (parent is null)
        {
            return null;
        }

        int index = IndexOfReference(parent.Children, element);
        if (index < 0)
        {
            return parent;
        }
        for (int candidate = index + 1; candidate < parent.Children.Count; candidate++)
        {
            if (SvgLayerPolicy.IsLayerElement(parent.Children[candidate].Name))
            {
                return parent.Children[candidate];
            }
        }
        for (int candidate = index - 1; candidate >= 0; candidate--)
        {
            if (SvgLayerPolicy.IsLayerElement(parent.Children[candidate].Name))
            {
                return parent.Children[candidate];
            }
        }

        return parent;
    }

    private static SvgElementNode? FindAfterDelete(
        SvgDocumentIndex rebuilt,
        SvgElementNode? preferred,
        SvgElementNode deleted,
        SvgElementNode? oldParent)
    {
        if (preferred is null)
        {
            return SvgSourceMutationUtilities.FindSvgRoot(rebuilt);
        }

        int offset = preferred.FullSpan.Start;
        if (preferred.FullSpan.Start > deleted.FullSpan.Start)
        {
            offset -= deleted.FullSpan.Length;
        }
        SvgElementNode? byOffset = rebuilt.FindElementAtOffset(
            Math.Clamp(offset + 1, 0, Math.Max(0, rebuilt.Elements
                .Max(element => element.FullSpan.End) - 1)));
        if (byOffset is not null
            && byOffset.Name.Equals(preferred.Name, StringComparison.Ordinal))
        {
            return byOffset;
        }

        SvgElementNode? byIdentity = rebuilt.FindBestMatch(preferred.Identity);
        if (byIdentity is not null)
        {
            return byIdentity;
        }

        return oldParent?.Name.Equals("svg", StringComparison.Ordinal) == true
            ? SvgSourceMutationUtilities.FindSvgRoot(rebuilt)
            : null;
    }

    private static int CountDescendants(SvgElementNode element) =>
        SvgSourceMutationUtilities.EnumerateSubtree(element).Count() - 1;

    private static bool HasMeaningfulContent(
        string source,
        SvgElementNode group)
    {
        int contentStart = group.StartTagSpan.End;
        int searchLength = group.FullSpan.End - contentStart;
        if (searchLength <= 0)
        {
            return false;
        }

        int closingStart = source.LastIndexOf(
            "</",
            group.FullSpan.End - 1,
            searchLength,
            StringComparison.Ordinal);
        if (closingStart < contentStart)
        {
            return false;
        }

        foreach (char character in source.AsSpan(
                     contentStart,
                     closingStart - contentStart))
        {
            if (!char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

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
