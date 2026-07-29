using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgPropertyViewModel : ObservableObject
{
    private string _value;
    private string _originalValue;
    private string _errorMessage = string.Empty;
    private string? _lastCommitAttemptValue;

    public SvgPropertyViewModel(
        SvgElementNode element,
        SvgPropertyDefinition definition,
        SvgAttributeSpan? attribute,
        IReadOnlyList<string>? suggestedValues = null)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Attribute = attribute;
        _value = ReadInitialValue(attribute, definition);
        _originalValue = _value;
        SuggestedValues = suggestedValues ?? [];
    }

    public SvgElementNode Element { get; }

    public SvgPropertyDefinition Definition { get; }

    public SvgAttributeSpan? Attribute { get; }

    public string Name => Definition.Name;

    public bool IsReadOnly => Definition.IsReadOnly;

    public bool IsPresent => Attribute is not null;

    public bool HasAllowedValues => Definition.AllowedValues is not null;

    public IReadOnlyList<string> AllowedValues =>
        Definition.AllowedValues ?? [];

    public bool HasSuggestedValues => SuggestedValues.Count > 0;

    public IReadOnlyList<string> SuggestedValues { get; }

    public string PresenceText => IsPresent ? "Existing attribute" : "Missing attribute";

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value ?? string.Empty))
            {
                _lastCommitAttemptValue = null;
            }
        }
    }

    public string OriginalValue => _originalValue;

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public void MarkApplied()
    {
        _originalValue = _value;
        _lastCommitAttemptValue = _value;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(OriginalValue));
    }

    public void Revert()
    {
        Value = _originalValue;
        _lastCommitAttemptValue = null;
        ErrorMessage = string.Empty;
    }

    public bool WasCurrentValueAlreadyAttempted =>
        _lastCommitAttemptValue?.Equals(
            _value,
            StringComparison.Ordinal) == true;

    public void MarkCommitAttempt()
    {
        _lastCommitAttemptValue = _value;
    }

    private static string ReadInitialValue(
        SvgAttributeSpan? attribute,
        SvgPropertyDefinition definition)
    {
        string rawValue = attribute?.RawValue ?? string.Empty;
        return definition.UsesFontFamilySuggestions
            && SvgXmlAttributeValueDecoder.TryDecode(
                rawValue,
                out string decoded)
                ? decoded
                : rawValue;
    }
}
