using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgTextContentViewModel : ObservableObject
{
    private string _value;
    private string _originalValue;
    private string _errorMessage = string.Empty;
    private string? _lastCommitAttemptValue;

    public SvgTextContentViewModel(
        SvgElementNode element,
        SvgDirectTextAnalysis analysis,
        bool isSessionLocked)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        ArgumentNullException.ThrowIfNull(analysis);
        _value = analysis.Content?.Text ?? string.Empty;
        _originalValue = _value;
        IsEnabled = analysis.IsEligible && !isSessionLocked;
        UnavailableReason = isSessionLocked
            ? "Unlock this layer or its parent group to edit text content."
            : analysis.UnavailableReason ?? string.Empty;
    }

    public SvgElementNode Element { get; }

    public string Name => "Text content";

    public string HelpText =>
        "Edits only the selected direct text content; Source remains authoritative.";

    public bool IsEnabled { get; }

    public string UnavailableReason { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value ?? string.Empty))
            {
                _lastCommitAttemptValue = null;
                OnPropertyChanged(nameof(HasUncommittedValue));
            }
        }
    }

    public string OriginalValue => _originalValue;

    public bool HasUncommittedValue => !_value.Equals(
        _originalValue,
        StringComparison.Ordinal);

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public bool WasCurrentValueAlreadyAttempted =>
        _lastCommitAttemptValue?.Equals(_value, StringComparison.Ordinal) == true;

    public void MarkCommitAttempt() => _lastCommitAttemptValue = _value;

    public void MarkApplied()
    {
        _originalValue = _value;
        _lastCommitAttemptValue = _value;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(OriginalValue));
        OnPropertyChanged(nameof(HasUncommittedValue));
    }

    public void Revert()
    {
        Value = _originalValue;
        _lastCommitAttemptValue = null;
        ErrorMessage = string.Empty;
    }
}
