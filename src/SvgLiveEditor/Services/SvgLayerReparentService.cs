using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayerReparentService
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgLayerOrderService _orderService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAuthoringAvailability GetDropAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode sourceElement,
        SvgElementNode target,
        SvgLayerDropPlacement placement,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceElement);
        ArgumentNullException.ThrowIfNull(target);

        if (placement is not (SvgLayerDropPlacement.Before
            or SvgLayerDropPlacement.After
            or SvgLayerDropPlacement.Inside))
        {
            return new SvgAuthoringAvailability(
                false,
                "The layer drop placement is invalid.");
        }

        return TryValidateElements(
            source,
            document,
            sourceElement,
            target,
            placement,
            isEffectivelyLocked,
            out _,
            out _,
            out string? error)
                ? new SvgAuthoringAvailability(true)
                : new SvgAuthoringAvailability(
                    false,
                    error ?? "The layer cannot be moved to that destination.");
    }

    public SvgAuthoringEditResult CreateDropEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode sourceElement,
        SvgElementNode target,
        SvgLayerDropPlacement placement,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceElement);
        ArgumentNullException.ThrowIfNull(target);

        SvgAuthoringAvailability availability = GetDropAvailability(
            source,
            document,
            sourceElement,
            target,
            placement,
            isEffectivelyLocked);
        if (!availability.CanExecute)
        {
            return SvgAuthoringEditResult.Invalid(
                availability.UnavailableReason
                ?? "The layer cannot be moved to that destination.");
        }
        if (!TryValidateElements(
                source,
                document,
                sourceElement,
                target,
                placement,
                isEffectivelyLocked,
                out SvgElementNode? sourceParent,
                out SvgElementNode? destinationParent,
                out string? error))
        {
            return SvgAuthoringEditResult.Invalid(
                error ?? "The layer cannot be moved to that destination.");
        }

        if (ReferenceEquals(sourceParent, destinationParent)
            && placement is not SvgLayerDropPlacement.Inside)
        {
            SvgLayerMoveEditResult sameParent = _orderService.CreateMoveEdit(
                source,
                document,
                sourceElement,
                target,
                placement);
            return sameParent.IsSuccess
                && sameParent.Edit is not null
                && sameParent.PreferredSelection is not null
                    ? SvgAuthoringEditResult.Success(
                        sameParent.Edit,
                        sameParent.PreferredSelection)
                    : SvgAuthoringEditResult.Invalid(
                        sameParent.ErrorMessage
                        ?? "The layer is already at that position.");
        }

        return CreateCrossParentEdit(
            source,
            document,
            sourceElement,
            target,
            placement,
            sourceParent!,
            destinationParent!);
    }

    public SvgAuthoringEditResult CreateMoveToRootFrontEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode sourceElement,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceElement);

        SvgAuthoringAvailability availability = GetMoveToRootAvailability(
            source,
            document,
            sourceElement,
            isEffectivelyLocked);
        if (!availability.CanExecute)
        {
            return SvgAuthoringEditResult.Invalid(
                availability.UnavailableReason
                ?? "The selected layer cannot be moved to the SVG root.");
        }
        SvgElementNode root = SvgSourceMutationUtilities.FindSvgRoot(document)!;

        string fragment = source.Substring(
            sourceElement.FullSpan.Start,
            sourceElement.FullSpan.Length);
        string intermediate = string.Concat(
            source.AsSpan(0, sourceElement.FullSpan.Start),
            source.AsSpan(sourceElement.FullSpan.End));
        SvgDocumentIndexResult intermediateIndex = _indexService.Build(intermediate);
        SvgElementNode? currentRoot = intermediateIndex.Document is null
            ? null
            : SvgSourceMutationUtilities.FindSvgRoot(intermediateIndex.Document);
        if (currentRoot is null)
        {
            return SvgAuthoringEditResult.Invalid(
                "The SVG root changed during the move.");
        }
        if (!SvgSourceMutationUtilities.TryInsertFrontmostChild(
                intermediate,
                currentRoot,
                fragment,
                out string candidate,
                out int insertedStart,
                out string? insertionError))
        {
            return SvgAuthoringEditResult.Invalid(
                insertionError ?? "The SVG root changed during the move.");
        }

        return CompleteMove(source, candidate, insertedStart, sourceElement.Name);
    }

    public SvgAuthoringAvailability GetMoveToRootAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? sourceElement,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        SvgElementNode? root = SvgSourceMutationUtilities.FindSvgRoot(document);
        SvgElementNode? sourceParent = sourceElement is null
            ? null
            : document.FindParent(sourceElement);
        if (root is null
            || sourceElement is null
            || sourceParent is null
            || !SvgLayerPolicy.IsLayerElement(sourceElement.Name)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, sourceElement))
        {
            return new SvgAuthoringAvailability(
                false,
                "Select visual artwork or a group to move to the SVG root.");
        }
        if (ReferenceEquals(sourceParent, root))
        {
            return new SvgAuthoringAvailability(
                false,
                "The selected layer is already under the SVG root.");
        }
        if (!SvgSourceMutationUtilities.IsCurrentElement(source, sourceElement)
            || !SvgSourceMutationUtilities.IsCurrentElement(source, root))
        {
            return new SvgAuthoringAvailability(
                false,
                "The source changed; select the layer again.");
        }
        if (isEffectivelyLocked?.Invoke(sourceElement) == true
            || isEffectivelyLocked?.Invoke(root) == true)
        {
            return new SvgAuthoringAvailability(
                false,
                "Unlock the source layer and destination context before moving it.");
        }
        string? contextError = GetContextChangeError(
            document,
            sourceParent,
            root);
        return contextError is null
            ? new SvgAuthoringAvailability(true)
            : new SvgAuthoringAvailability(false, contextError);
    }

    private bool TryValidateElements(
        string source,
        SvgDocumentIndex document,
        SvgElementNode sourceElement,
        SvgElementNode target,
        SvgLayerDropPlacement placement,
        Func<SvgElementNode, bool>? isEffectivelyLocked,
        out SvgElementNode? sourceParent,
        out SvgElementNode? destinationParent,
        out string? error)
    {
        sourceParent = document.FindParent(sourceElement);
        destinationParent = placement == SvgLayerDropPlacement.Inside
            ? target
            : document.FindParent(target);
        error = null;
        if (ReferenceEquals(sourceElement, target))
        {
            error = "A layer cannot be moved relative to or inside itself.";
            return false;
        }
        if (sourceElement.Name.Equals("svg", StringComparison.Ordinal)
            || !SvgLayerPolicy.IsLayerElement(sourceElement.Name)
            || !SvgLayerPolicy.IsLayerElement(target.Name)
            || sourceParent is null
            || destinationParent is null
            || destinationParent.Name is not ("svg" or "g")
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, sourceElement)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, destinationParent))
        {
            error = "The source or destination is not an eligible visual layer context.";
            return false;
        }
        if (placement == SvgLayerDropPlacement.Inside
            && !SvgLayerPolicy.IsGroup(target.Name))
        {
            error = "Only a group can accept an inside drop.";
            return false;
        }
        if (SvgSourceMutationUtilities.IsDescendantOrSelf(sourceElement, target))
        {
            error = "A group cannot be moved into one of its descendants.";
            return false;
        }
        if (!SvgSourceMutationUtilities.IsCurrentElement(source, sourceElement)
            || !SvgSourceMutationUtilities.IsCurrentElement(source, target)
            || !SvgSourceMutationUtilities.IsCurrentElement(source, destinationParent))
        {
            error = "The source changed; start the layer drag again.";
            return false;
        }
        if (isEffectivelyLocked?.Invoke(sourceElement) == true
            || isEffectivelyLocked?.Invoke(target) == true
            || isEffectivelyLocked?.Invoke(destinationParent) == true)
        {
            error = "Unlock the source layer and destination context before moving it.";
            return false;
        }

        if (!ReferenceEquals(sourceParent, destinationParent))
        {
            error = GetContextChangeError(
                document,
                sourceParent,
                destinationParent);
            if (error is not null)
            {
                return false;
            }
        }

        return true;
    }

    private SvgAuthoringEditResult CreateCrossParentEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode sourceElement,
        SvgElementNode target,
        SvgLayerDropPlacement placement,
        SvgElementNode sourceParent,
        SvgElementNode destinationParent)
    {
        string fragment = source.Substring(
            sourceElement.FullSpan.Start,
            sourceElement.FullSpan.Length);
        string intermediate = string.Concat(
            source.AsSpan(0, sourceElement.FullSpan.Start),
            source.AsSpan(sourceElement.FullSpan.End));
        SvgDocumentIndexResult intermediateResult = _indexService.Build(intermediate);
        if (intermediateResult.Document is not SvgDocumentIndex intermediateDocument)
        {
            return SvgAuthoringEditResult.Invalid(
                intermediateResult.IndexError
                ?? "The source could not be indexed after removing the layer.");
        }

        int targetOffset = target.FullSpan.Start
            - (target.FullSpan.Start > sourceElement.FullSpan.Start
                ? sourceElement.FullSpan.Length
                : 0);
        SvgElementNode? currentTarget = intermediateDocument.FindElementAtOffset(
            Math.Clamp(targetOffset + 1, 0, intermediate.Length - 1));
        if (currentTarget is null
            || !currentTarget.Name.Equals(target.Name, StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid(
                "The drop target could not be identified after the source move.");
        }

        string candidate;
        int insertedStart;
        if (placement == SvgLayerDropPlacement.Inside)
        {
            if (!SvgSourceMutationUtilities.TryInsertFrontmostChild(
                    intermediate,
                    currentTarget,
                    fragment,
                    out candidate,
                    out insertedStart,
                    out string? insertionError))
            {
                return SvgAuthoringEditResult.Invalid(
                    insertionError ?? "The layer could not be inserted into the group.");
            }
        }
        else if (placement == SvgLayerDropPlacement.Before)
        {
            candidate = SvgSourceMutationUtilities.InsertAdjacentAfter(
                intermediate,
                currentTarget,
                fragment,
                out insertedStart);
        }
        else
        {
            candidate = SvgSourceMutationUtilities.InsertAdjacentBefore(
                intermediate,
                currentTarget,
                fragment,
                out insertedStart);
        }

        _ = sourceParent;
        _ = destinationParent;
        return CompleteMove(source, candidate, insertedStart, sourceElement.Name);
    }

    private SvgAuthoringEditResult CompleteMove(
        string source,
        string candidate,
        int insertedStart,
        string expectedElementName)
    {
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        if (!validation.IsValid || rebuilt.Document is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The reparent would make the SVG invalid: {validation.Message}");
        }

        SvgElementNode? moved = rebuilt.Document.FindElementAtOffset(
            Math.Min(insertedStart + 1, candidate.Length - 1));
        if (moved is null
            || !moved.Name.Equals(expectedElementName, StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid(
                "The moved layer could not be identified safely.");
        }

        return SvgAuthoringEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate),
            moved.Identity);
    }

    private static string? GetContextChangeError(
        SvgDocumentIndex document,
        SvgElementNode sourceParent,
        SvgElementNode destinationParent)
    {
        IReadOnlyList<SvgElementNode> sourceAncestors = GetAncestors(
            document,
            sourceParent);
        IReadOnlyList<SvgElementNode> destinationAncestors = GetAncestors(
            document,
            destinationParent);
        int common = 0;
        while (common < sourceAncestors.Count
            && common < destinationAncestors.Count
            && ReferenceEquals(
                sourceAncestors[common],
                destinationAncestors[common]))
        {
            common++;
        }

        foreach (SvgElementNode context in sourceAncestors.Skip(common)
                     .Concat(destinationAncestors.Skip(common)))
        {
            if (!context.Name.Equals("g", StringComparison.Ordinal))
            {
                return "The move crosses a non-group rendering context and was rejected.";
            }

            SvgAttributeSpan? semanticAttribute = context.Attributes
                .FirstOrDefault(attribute => !attribute.Name.Equals(
                    "id",
                    StringComparison.Ordinal));
            if (semanticAttribute is not null)
            {
                return $"The move could change inherited '{semanticAttribute.QualifiedName}' semantics on {context.DisplayLabel}; Stage 1 does not flatten transforms, styles, or effects.";
            }
        }

        return null;
    }

    private static IReadOnlyList<SvgElementNode> GetAncestors(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        List<SvgElementNode> result = [];
        for (SvgElementNode? current = element;
             current is not null;
             current = document.FindParent(current))
        {
            result.Add(current);
        }
        result.Reverse();
        return result;
    }
}
