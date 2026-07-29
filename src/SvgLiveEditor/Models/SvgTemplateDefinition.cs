namespace SvgLiveEditor.Models;

public sealed record SvgTemplateDefinition(
    string Id,
    string Name,
    string Category,
    string Dimensions,
    string Description,
    string Source);
