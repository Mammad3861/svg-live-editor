using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgMultiSelectionService
{
    public const int MaximumSelectionCount = 128;

    public SvgMultiSelectionState Replace(
        long sourceRevision,
        SvgElementIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateRevision(sourceRevision);
        return new SvgMultiSelectionState(
            sourceRevision,
            [identity],
            identity,
            identity);
    }

    public SvgMultiSelectionState Clear(long sourceRevision)
    {
        ValidateRevision(sourceRevision);
        return SvgMultiSelectionState.Empty(sourceRevision);
    }

    public SvgMultiSelectionChange Toggle(
        SvgMultiSelectionState current,
        long expectedSourceRevision,
        SvgElementIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(identity);
        if (!IsCurrent(current, expectedSourceRevision))
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                "The source changed; start the selection again.");
        }

        List<SvgElementIdentity> identities = [.. current.Identities];
        int existing = identities.IndexOf(identity);
        if (existing >= 0)
        {
            identities.RemoveAt(existing);
            SvgElementIdentity? primary = current.Primary == identity
                ? identities.LastOrDefault()
                : current.Primary;
            SvgElementIdentity? anchor = current.Anchor == identity
                ? primary
                : current.Anchor;
            return SvgMultiSelectionChange.Success(
                new SvgMultiSelectionState(
                    expectedSourceRevision,
                    identities,
                    primary,
                    anchor));
        }

        if (identities.Count >= MaximumSelectionCount)
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                $"At most {MaximumSelectionCount} elements can be selected.");
        }

        identities.Add(identity);
        return SvgMultiSelectionChange.Success(
            new SvgMultiSelectionState(
                expectedSourceRevision,
                identities,
                identity,
                identity));
    }

    public SvgMultiSelectionChange SelectRange(
        SvgMultiSelectionState current,
        long expectedSourceRevision,
        IReadOnlyList<SvgElementIdentity> orderedSiblings,
        SvgElementIdentity target)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(orderedSiblings);
        ArgumentNullException.ThrowIfNull(target);
        if (!IsCurrent(current, expectedSourceRevision))
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                "The source changed; start the range selection again.");
        }
        if (current.Anchor is not SvgElementIdentity anchor)
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                "Select an anchor layer before extending a range.");
        }
        if (orderedSiblings.Count == 0
            || orderedSiblings.Count > MaximumSelectionCount
            || orderedSiblings.Distinct().Count() != orderedSiblings.Count)
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                "The visible sibling range is invalid or too large.");
        }

        int anchorIndex = IndexOf(orderedSiblings, anchor);
        int targetIndex = IndexOf(orderedSiblings, target);
        if (anchorIndex < 0 || targetIndex < 0)
        {
            return SvgMultiSelectionChange.Invalid(
                current,
                "Range selection cannot cross a collapsed or different layer container.");
        }

        int start = Math.Min(anchorIndex, targetIndex);
        int count = Math.Abs(targetIndex - anchorIndex) + 1;
        SvgElementIdentity[] range = orderedSiblings
            .Skip(start)
            .Take(count)
            .ToArray();
        return SvgMultiSelectionChange.Success(
            new SvgMultiSelectionState(
                expectedSourceRevision,
                range,
                target,
                anchor));
    }

    public SvgMultiSelectionState Reconcile(
        SvgMultiSelectionState current,
        long newSourceRevision,
        SvgDocumentIndex document)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(document);
        ValidateRevision(newSourceRevision);
        if (current.Identities.Count > MaximumSelectionCount)
        {
            return SvgMultiSelectionState.Empty(newSourceRevision);
        }

        List<SvgElementIdentity> identities = [];
        foreach (SvgElementIdentity identity in current.Identities)
        {
            SvgElementIdentity? currentIdentity = document
                .FindBestMatch(identity)?
                .Identity;
            if (currentIdentity is not null
                && !identities.Contains(currentIdentity))
            {
                identities.Add(currentIdentity);
            }
        }

        SvgElementIdentity? primary = ReconcileIdentity(
            current.Primary,
            document,
            identities);
        SvgElementIdentity? anchor = ReconcileIdentity(
            current.Anchor,
            document,
            identities);
        primary ??= identities.LastOrDefault();
        anchor ??= primary;
        return new SvgMultiSelectionState(
            newSourceRevision,
            identities,
            primary,
            anchor);
    }

    private static SvgElementIdentity? ReconcileIdentity(
        SvgElementIdentity? identity,
        SvgDocumentIndex document,
        IReadOnlyList<SvgElementIdentity> reconciled)
    {
        SvgElementIdentity? candidate = identity is null
            ? null
            : document.FindBestMatch(identity)?.Identity;
        return candidate is not null && reconciled.Contains(candidate)
            ? candidate
            : null;
    }

    private static bool IsCurrent(
        SvgMultiSelectionState current,
        long expectedSourceRevision) =>
        expectedSourceRevision >= 0
        && current.SourceRevision == expectedSourceRevision
        && current.Identities.Count <= MaximumSelectionCount
        && current.Identities.Distinct().Count() == current.Identities.Count
        && (current.Primary is null
            ? current.Identities.Count == 0
            : current.Identities.Contains(current.Primary))
        && (current.Anchor is null
            || current.Identities.Contains(current.Anchor));

    private static int IndexOf(
        IReadOnlyList<SvgElementIdentity> items,
        SvgElementIdentity identity)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index] == identity)
            {
                return index;
            }
        }
        return -1;
    }

    private static void ValidateRevision(long sourceRevision)
    {
        if (sourceRevision is < 0
            or > PreviewPageMessageBuilder.MaximumJavaScriptSafeInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }
    }
}
