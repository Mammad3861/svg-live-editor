using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgElementDuplicateService
{
    private const int MaximumGeneratedIdLength = 128;
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
            || !SvgLayerPolicy.IsLayerElement(element.Name)
            || SvgLayerPolicy.IsInsideDefinitionContainer(document, element))
        {
            return new SvgAuthoringAvailability(
                false,
                "Select visual artwork or a group to duplicate.");
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
                "Unlock the layer and its parent group before duplicating it.");
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
                ?? "The element cannot be duplicated.");
        }

        SvgElementNode[] subtree = SvgSourceMutationUtilities
            .EnumerateSubtree(element)
            .ToArray();
        Dictionary<string, int> allIdCounts = document.Elements
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        SvgAttributeSpan[] subtreeIds = subtree
            .Select(item => item.FindAttribute("id"))
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!)
            .ToArray();
        string? duplicateSubtreeId = subtreeIds
            .GroupBy(attribute => attribute.RawValue, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Take(2).Count() > 1)
            ?.Key;
        if (duplicateSubtreeId is not null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The selected subtree contains the duplicate ID '{duplicateSubtreeId}', so its references cannot be remapped safely.");
        }

        IReadOnlyList<SvgLocalReference> references;
        try
        {
            references = _referenceService.FindReferences(subtree);
        }
        catch (InvalidOperationException exception)
        {
            return SvgAuthoringEditResult.Invalid(exception.Message);
        }
        foreach (SvgLocalReference reference in references)
        {
            if (!allIdCounts.TryGetValue(reference.TargetId, out int count)
                || count != 1)
            {
                return SvgAuthoringEditResult.Invalid(
                    $"The selected subtree refers to missing or ambiguous ID '{reference.TargetId}'. Duplicate was not created.");
            }
        }

        Dictionary<string, string> idMap = CreateIdMap(
            subtreeIds,
            document);
        string fragment = source.Substring(
            element.FullSpan.Start,
            element.FullSpan.Length);
        List<SourceTextEdit> fragmentEdits = [];
        foreach (SvgElementNode subtreeElement in subtree)
        {
            SvgAttributeSpan? idAttribute = subtreeElement.FindAttribute("id");
            if (idAttribute is not null)
            {
                fragmentEdits.Add(new SourceTextEdit(
                    idAttribute.ValueSpan.Start - element.FullSpan.Start,
                    idAttribute.ValueSpan.Length,
                    idMap[idAttribute.RawValue]));
            }

            foreach (SvgAttributeSpan attribute in subtreeElement.Attributes)
            {
                if (attribute.Name.Equals("id", StringComparison.Ordinal))
                {
                    continue;
                }

                string rewritten;
                try
                {
                    rewritten = _referenceService.RewriteReferences(
                        attribute,
                        idMap);
                }
                catch (InvalidOperationException exception)
                {
                    return SvgAuthoringEditResult.Invalid(exception.Message);
                }
                if (!rewritten.Equals(attribute.RawValue, StringComparison.Ordinal))
                {
                    fragmentEdits.Add(new SourceTextEdit(
                        attribute.ValueSpan.Start - element.FullSpan.Start,
                        attribute.ValueSpan.Length,
                        rewritten));
                }
            }
        }
        foreach (SourceTextEdit edit in fragmentEdits
                     .OrderByDescending(edit => edit.Start))
        {
            fragment = edit.Apply(fragment);
        }

        string candidate = SvgSourceMutationUtilities.InsertAdjacentAfter(
            source,
            element,
            fragment,
            out int insertedStart);
        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        if (!validation.IsValid || rebuilt.Document is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The duplicate would make the SVG invalid: {validation.Message}");
        }

        SvgElementNode? duplicate = rebuilt.Document.FindElementAtOffset(
            Math.Min(insertedStart + 1, candidate.Length - 1));
        if (duplicate is null || !duplicate.Name.Equals(
                element.Name,
                StringComparison.Ordinal))
        {
            return SvgAuthoringEditResult.Invalid(
                "The duplicate could not be identified safely after insertion.");
        }

        return SvgAuthoringEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate),
            duplicate.Identity);
    }

    private static Dictionary<string, string> CreateIdMap(
        IEnumerable<SvgAttributeSpan> idAttributes,
        SvgDocumentIndex document)
    {
        HashSet<string> reserved = document.Elements
            .Select(element => element.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (SvgAttributeSpan attribute in idAttributes)
        {
            string stem = CreateCopyStem(attribute.RawValue);
            string candidate = stem;
            int suffix = 2;
            while (reserved.Contains(candidate))
            {
                string suffixText = $"-{suffix++}";
                candidate = string.Concat(
                    stem.AsSpan(
                        0,
                        Math.Min(
                            stem.Length,
                            MaximumGeneratedIdLength - suffixText.Length)),
                    suffixText);
            }
            reserved.Add(candidate);
            result.Add(attribute.RawValue, candidate);
        }

        return result;
    }

    private static string CreateCopyStem(string id)
    {
        StringBuilder safe = new(Math.Min(id.Length, MaximumGeneratedIdLength));
        foreach (char character in id)
        {
            char next = char.IsAsciiLetterOrDigit(character)
                || character is '_' or '-' or '.' or ':'
                    ? character
                    : '-';
            if (safe.Length == 0 && !char.IsAsciiLetter(next) && next != '_')
            {
                safe.Append("item-");
            }
            if (safe.Length < MaximumGeneratedIdLength - "-copy".Length)
            {
                safe.Append(next);
            }
        }
        if (safe.Length == 0)
        {
            safe.Append("item");
        }

        safe.Append("-copy");
        return safe.ToString();
    }
}
