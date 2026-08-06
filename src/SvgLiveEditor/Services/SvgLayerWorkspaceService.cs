using System.Net;
using System.Security.Cryptography;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayerWorkspaceService
{
    private const int MaximumLabelLength = 36;
    private readonly SvgLayerVisibilityService _visibilityService = new();
    private readonly HashSet<string> _lockedIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VisibilityOwnership>
        _ownedHiddenById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SessionEntry> _entriesByPath =
        new(StringComparer.Ordinal);
    private IReadOnlyList<SessionEntry> _previousEntries = [];
    private SvgLayerWorkspace _workspace = EmptyWorkspace;

    public SvgLayerWorkspace Workspace => _workspace;

    public void BeginDocumentSession()
    {
        _lockedIds.Clear();
        _ownedHiddenById.Clear();
        _entriesByPath.Clear();
        _previousEntries = [];
        _workspace = EmptyWorkspace;
    }

    public SvgLayerWorkspace Build(
        SvgDocumentIndex document,
        string source,
        SvgVisualDocument? visualDocument = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);

        SvgElementNode[] layerElements = document.Elements
            .Where(element => SvgLayerPolicy.IsLayerElement(element.Name))
            .Where(element => !SvgLayerPolicy.IsInsideDefinitionContainer(
                document,
                element))
            .ToArray();
        IReadOnlyDictionary<SvgElementNode, string> opaqueIds =
            ReconcileIdentities(layerElements, source);
        Dictionary<string, SvgLayerItem> byPath =
            new(StringComparer.Ordinal);
        Dictionary<string, SvgLayerItem> byOpaqueId =
            new(StringComparer.Ordinal);

        SvgLayerItem CreateItem(SvgElementNode element, bool ancestorLocked)
        {
            string opaqueId = opaqueIds[element];
            bool isLocked = _lockedIds.Contains(opaqueId);
            bool effectiveLock = ancestorLocked || isLocked;
            SvgLayerItem[] children = element.Children
                .Where(child => opaqueIds.ContainsKey(child))
                .Reverse()
                .Select(child => CreateItem(child, effectiveLock))
                .ToArray();
            SvgVisualElement? visual = visualDocument?.FindElement(
                element.Identity);
            bool inspectionOnly = !SvgLayerPolicy.IsGroup(element.Name)
                && (visual is null || !visual.IsMovable);
            SvgLayerItem item = new(
                opaqueId,
                element,
                CreateLabel(element, source),
                SvgLayerPolicy.IsGroup(element.Name),
                inspectionOnly,
                isLocked,
                effectiveLock,
                _visibilityService.Analyze(
                    document,
                    element,
                    _ownedHiddenById.ContainsKey(opaqueId)),
                children);
            byPath.Add(element.StructuralPath, item);
            byOpaqueId.Add(opaqueId, item);
            return item;
        }

        SvgElementNode? svgRoot = document.Roots.FirstOrDefault(element =>
            element.Name.Equals("svg", StringComparison.Ordinal));
        SvgLayerItem[] roots = (svgRoot?.Children ?? [])
            .Where(child => opaqueIds.ContainsKey(child))
            .Reverse()
            .Select(child => CreateItem(child, ancestorLocked: false))
            .ToArray();
        _workspace = new SvgLayerWorkspace(roots, byPath, byOpaqueId);
        return _workspace;
    }

    public bool ToggleLock(string opaqueId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueId);
        if (!_workspace.ItemsByOpaqueId.ContainsKey(opaqueId))
        {
            return false;
        }

        if (!_lockedIds.Add(opaqueId))
        {
            _lockedIds.Remove(opaqueId);
        }

        return true;
    }

    public bool IsEffectivelyLocked(
        SvgDocumentIndex document,
        SvgElementNode element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        for (SvgElementNode? current = element;
             current is not null;
             current = document.FindParent(current))
        {
            if (_entriesByPath.TryGetValue(
                    current.StructuralPath,
                    out SessionEntry? entry)
                && _lockedIds.Contains(entry.OpaqueId))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsHiddenAttributeOwned(string opaqueId) =>
        _ownedHiddenById.ContainsKey(opaqueId);

    public void SetHiddenAttributeOwned(string opaqueId, bool isOwned)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueId);
        if (isOwned)
        {
            SessionEntry? current = _previousEntries.FirstOrDefault(entry =>
                entry.OpaqueId.Equals(opaqueId, StringComparison.Ordinal));
            if (current is not null)
            {
                _ownedHiddenById[opaqueId] = new VisibilityOwnership(
                    current.Fingerprint,
                    HiddenFingerprint: null);
            }
        }
        // Keep the visible/hidden fingerprint pair so Undo/Redo of a show
        // operation remains recognizable without treating later source edits
        // as app-owned.
    }

    private IReadOnlyDictionary<SvgElementNode, string> ReconcileIdentities(
        IReadOnlyList<SvgElementNode> elements,
        string source)
    {
        SessionEntry[] current = elements
            .Select(element => new SessionEntry(
                string.Empty,
                element.Name,
                element.Id,
                element.StructuralPath,
                CreateFingerprint(element, source)))
            .ToArray();
        Dictionary<(string Name, string Fingerprint), int>
            currentFingerprintCounts = current
                .GroupBy(entry => (entry.Name, entry.Fingerprint))
                .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<(string Name, string Id), int> currentIdCounts = current
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .GroupBy(entry => (entry.Name, entry.Id!))
            .ToDictionary(group => group.Key, group => group.Count());
        IReadOnlyDictionary<(string Name, string Fingerprint), SessionEntry>
            previousByFingerprint = CreateUniqueMap(
                _previousEntries,
                entry => (entry.Name, entry.Fingerprint));
        IReadOnlyDictionary<(string Name, string Id), SessionEntry>
            previousById = CreateUniqueMap(
                _previousEntries.Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Id)),
                entry => (entry.Name, entry.Id!));
        IReadOnlyDictionary<(string Name, string Path), SessionEntry>
            previousByPath = CreateUniqueMap(
                _previousEntries,
                entry => (entry.Name, entry.StructuralPath));
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        Dictionary<SvgElementNode, string> result = [];
        _entriesByPath.Clear();

        for (int index = 0; index < elements.Count; index++)
        {
            SessionEntry candidate = current[index];
            SessionEntry? match = null;
            (string Name, string Fingerprint) fingerprintKey =
                (candidate.Name, candidate.Fingerprint);
            if (currentFingerprintCounts[fingerprintKey] == 1)
            {
                previousByFingerprint.TryGetValue(
                    fingerprintKey,
                    out match);
            }
            if ((match is null || usedIds.Contains(match.OpaqueId))
                && !string.IsNullOrWhiteSpace(candidate.Id))
            {
                (string Name, string Id) idKey =
                    (candidate.Name, candidate.Id!);
                if (currentIdCounts[idKey] == 1)
                {
                    previousById.TryGetValue(idKey, out match);
                }
            }
            if (match is null || usedIds.Contains(match.OpaqueId))
            {
                previousByPath.TryGetValue(
                    (candidate.Name, candidate.StructuralPath),
                    out match);
            }
            if (match is not null && usedIds.Contains(match.OpaqueId))
            {
                match = null;
            }
            string opaqueId = match?.OpaqueId
                ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            usedIds.Add(opaqueId);
            SessionEntry assigned = candidate with { OpaqueId = opaqueId };
            _entriesByPath.Add(assigned.StructuralPath, assigned);
            result.Add(elements[index], opaqueId);
            current[index] = assigned;
        }

        _previousEntries = current;
        _lockedIds.IntersectWith(usedIds);
        ReconcileVisibilityOwnership(current, usedIds);
        return result;
    }

    private void ReconcileVisibilityOwnership(
        IReadOnlyList<SessionEntry> current,
        IReadOnlySet<string> usedIds)
    {
        Dictionary<string, SessionEntry> currentById = current.ToDictionary(
            entry => entry.OpaqueId,
            StringComparer.Ordinal);
        foreach (string opaqueId in _ownedHiddenById.Keys.ToArray())
        {
            if (!usedIds.Contains(opaqueId)
                || !currentById.TryGetValue(
                    opaqueId,
                    out SessionEntry? entry))
            {
                _ownedHiddenById.Remove(opaqueId);
                continue;
            }

            VisibilityOwnership ownership = _ownedHiddenById[opaqueId];
            if (ownership.HiddenFingerprint is null)
            {
                if (entry.Fingerprint.Equals(
                        ownership.VisibleFingerprint,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                _ownedHiddenById[opaqueId] = ownership with
                {
                    HiddenFingerprint = entry.Fingerprint
                };
                continue;
            }

            if (!entry.Fingerprint.Equals(
                    ownership.VisibleFingerprint,
                    StringComparison.Ordinal)
                && !entry.Fingerprint.Equals(
                    ownership.HiddenFingerprint,
                    StringComparison.Ordinal))
            {
                _ownedHiddenById.Remove(opaqueId);
            }
        }
    }

    private static IReadOnlyDictionary<TKey, SessionEntry> CreateUniqueMap<TKey>(
        IEnumerable<SessionEntry> entries,
        Func<SessionEntry, TKey> keySelector)
        where TKey : notnull
    {
        return entries
            .GroupBy(keySelector)
            .Where(group => group.Take(2).Count() == 1)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static string CreateFingerprint(
        SvgElementNode element,
        string source)
    {
        const int sampleLength = 160;
        SourceSpan span = element.FullSpan;
        if (span.Start < 0
            || span.Length <= 0
            || span.Start > source.Length - span.Length)
        {
            return element.StructuralPath;
        }

        ReadOnlySpan<char> full = source.AsSpan(span.Start, span.Length);
        ReadOnlySpan<char> start = full[..Math.Min(full.Length, sampleLength)];
        ReadOnlySpan<char> end = full.Length <= sampleLength
            ? ReadOnlySpan<char>.Empty
            : full[^Math.Min(full.Length - sampleLength, sampleLength)..];
        string value = string.Concat(
            element.Name,
            "\n",
            span.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "\n",
            start.ToString(),
            "\n",
            end.ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string CreateLabel(
        SvgElementNode element,
        string source)
    {
        if (!string.IsNullOrWhiteSpace(element.Id))
        {
            string id = DecodeAndCollapse(element.Id!);
            return $"{element.Name} #{Truncate(id)}";
        }

        if (element.Name.Equals("text", StringComparison.Ordinal))
        {
            string text = ExtractText(element, source);
            if (text.Length > 0)
            {
                return $"text · {Truncate(text)}";
            }
        }

        return SvgLayerPolicy.IsGroup(element.Name)
            ? "Group"
            : element.Name;
    }

    private static string ExtractText(
        SvgElementNode element,
        string source)
    {
        if (element.FullSpan.Start < 0
            || element.FullSpan.Start > source.Length - element.FullSpan.Length)
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = source.AsSpan(
            element.StartTagSpan.End,
            element.FullSpan.End - element.StartTagSpan.End);
        StringBuilder text = new();
        bool insideTag = false;
        foreach (char character in span)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }
            if (character == '>')
            {
                insideTag = false;
                continue;
            }
            if (!insideTag)
            {
                text.Append(character);
            }
        }

        return DecodeAndCollapse(text.ToString());
    }

    private static string DecodeAndCollapse(string value)
    {
        string decoded = WebUtility.HtmlDecode(value);
        return string.Join(
            " ",
            decoded.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries));
    }

    private static string Truncate(string value) =>
        value.Length <= MaximumLabelLength
            ? value
            : $"{value[..(MaximumLabelLength - 1)]}…";

    private sealed record SessionEntry(
        string OpaqueId,
        string Name,
        string? Id,
        string StructuralPath,
        string Fingerprint);

    private sealed record VisibilityOwnership(
        string VisibleFingerprint,
        string? HiddenFingerprint);

    private static SvgLayerWorkspace EmptyWorkspace { get; } = new(
        [],
        new Dictionary<string, SvgLayerItem>(StringComparer.Ordinal),
        new Dictionary<string, SvgLayerItem>(StringComparer.Ordinal));
}
