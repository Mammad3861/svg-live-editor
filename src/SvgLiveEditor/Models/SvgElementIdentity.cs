namespace SvgLiveEditor.Models;

public sealed record SvgElementIdentity(
    string Name,
    string? Id,
    string StructuralPath);
