namespace SvgLiveEditor.Models;

public sealed class SvgElementNode
{
    public SvgElementNode(
        string name,
        string qualifiedName,
        string structuralPath,
        int depth,
        SourceSpan startTagSpan,
        SourceSpan fullSpan,
        IReadOnlyList<SvgAttributeSpan> attributes,
        IReadOnlyList<SvgElementNode> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(structuralPath);
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(children);

        Name = name;
        QualifiedName = qualifiedName;
        StructuralPath = structuralPath;
        Depth = depth;
        StartTagSpan = startTagSpan;
        FullSpan = fullSpan;
        Attributes = attributes;
        Children = children;
    }

    public string Name { get; }

    public string QualifiedName { get; }

    public string StructuralPath { get; }

    public int Depth { get; }

    public SourceSpan StartTagSpan { get; }

    public SourceSpan FullSpan { get; }

    public IReadOnlyList<SvgAttributeSpan> Attributes { get; }

    public IReadOnlyList<SvgElementNode> Children { get; }

    public string? Id => FindAttribute("id")?.RawValue;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Id)
        ? Name
        : $"{Name} #{Id}";

    public SvgElementIdentity Identity => new(Name, Id, StructuralPath);

    public SvgAttributeSpan? FindAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Attributes.FirstOrDefault(attribute =>
            attribute.QualifiedName.Equals(name, StringComparison.Ordinal));
    }
}
