namespace SvgLiveEditor.Models;

public sealed class SvgDocumentIndex
{
    private readonly IReadOnlyDictionary<SvgElementNode, SvgElementNode>
        _parents;

    public SvgDocumentIndex(
        IReadOnlyList<SvgElementNode> roots,
        IReadOnlyList<SvgElementNode> elements)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(elements);

        Roots = roots;
        Elements = elements;
        Dictionary<SvgElementNode, SvgElementNode> parents = [];
        foreach (SvgElementNode parent in elements)
        {
            foreach (SvgElementNode child in parent.Children)
            {
                parents.Add(child, parent);
            }
        }

        _parents = parents;
    }

    public IReadOnlyList<SvgElementNode> Roots { get; }

    public IReadOnlyList<SvgElementNode> Elements { get; }

    public SvgElementNode? FindParent(SvgElementNode element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _parents.GetValueOrDefault(element);
    }

    public SvgElementNode? FindElementAtOffset(int offset)
    {
        SvgElementNode? match = FindContaining(offset);
        return match ?? (offset > 0 ? FindContaining(offset - 1) : null);
    }

    public SvgElementNode? FindBestMatch(SvgElementIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!string.IsNullOrWhiteSpace(identity.Id))
        {
            SvgElementNode[] idMatches = Elements
                .Where(element =>
                    element.Name.Equals(identity.Name, StringComparison.Ordinal)
                    && element.Id?.Equals(identity.Id, StringComparison.Ordinal) == true)
                .Take(2)
                .ToArray();
            if (idMatches.Length == 1)
            {
                return idMatches[0];
            }
        }

        return Elements.FirstOrDefault(element =>
            element.Name.Equals(identity.Name, StringComparison.Ordinal)
            && element.StructuralPath.Equals(identity.StructuralPath, StringComparison.Ordinal));
    }

    private SvgElementNode? FindContaining(int offset)
    {
        return Elements
            .Where(element => element.FullSpan.Contains(offset))
            .OrderByDescending(element => element.Depth)
            .ThenBy(element => element.FullSpan.Length)
            .FirstOrDefault();
    }
}
