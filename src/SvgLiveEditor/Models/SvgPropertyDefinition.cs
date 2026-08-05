namespace SvgLiveEditor.Models;

public sealed record SvgPropertyDefinition(
    string Name,
    bool IsReadOnly = false,
    bool RemoveWhenEmpty = false,
    IReadOnlyList<string>? AllowedValues = null,
    bool UsesFontFamilySuggestions = false,
    string HelpText = "");
