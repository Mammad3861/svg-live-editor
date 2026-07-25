namespace SvgLiveEditor.Models;

public sealed record SvgAttributeSpan(
    string Name,
    string QualifiedName,
    SourceSpan NameSpan,
    SourceSpan ValueSpan,
    char Quote,
    string RawValue);
