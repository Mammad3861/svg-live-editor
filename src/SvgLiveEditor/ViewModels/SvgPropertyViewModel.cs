using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgPropertyViewModel : ObservableObject
{
    private string _value;
    private string _originalValue;
    private string _errorMessage = string.Empty;

    public SvgPropertyViewModel(
        SvgElementNode element,
        SvgPropertyDefinition definition,
        SvgAttributeSpan? attribute)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Attribute = attribute;
        _value = attribute?.RawValue ?? string.Empty;
        _originalValue = _value;
    }

    public SvgElementNode Element { get; }

    public SvgPropertyDefinition Definition { get; }

    public SvgAttributeSpan? Attribute { get; }

    public string Name => Definition.Name;

    public bool IsReadOnly => Definition.IsReadOnly;

    public bool IsPresent => Attribute is not null;

    public string PresenceText => IsPresent ? "Existing attribute" : "Missing attribute";

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value ?? string.Empty);
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
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(OriginalValue));
    }

    public void Revert()
    {
        Value = _originalValue;
        ErrorMessage = string.Empty;
    }
}
