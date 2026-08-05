using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgOpacityViewModel : ObservableObject
{
    private double _percent;
    private double _originalPercent;
    private string _text;
    private string _errorMessage = string.Empty;
    private string? _lastCommitAttempt;

    public SvgOpacityViewModel(
        SvgElementNode element,
        SvgOpacityControlState state)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        ArgumentNullException.ThrowIfNull(state);
        IsEnabled = state.IsEnabled;
        UnavailableReason = state.UnavailableReason ?? string.Empty;
        Advisory = state.Advisory ?? string.Empty;
        _percent = state.Percent;
        _originalPercent = state.Percent;
        _text = Format(state.Percent);
    }

    public SvgElementNode Element { get; }

    public bool IsEnabled { get; }

    public string UnavailableReason { get; }

    public string Advisory { get; }

    public double Percent
    {
        get => _percent;
        set
        {
            double bounded = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _percent, bounded))
            {
                _text = Format(bounded);
                _lastCommitAttempt = null;
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(HasUncommittedValue));
            }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                _lastCommitAttempt = null;
                OnPropertyChanged(nameof(HasUncommittedValue));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public bool HasUncommittedValue => !_text.Equals(
        Format(_originalPercent),
        StringComparison.Ordinal);

    public bool WasCurrentTextAlreadyAttempted =>
        _lastCommitAttempt?.Equals(Text, StringComparison.Ordinal) == true;

    public void MarkCommitAttempt() => _lastCommitAttempt = Text;

    public bool TryReadPercent(out double percent)
    {
        string normalized = Text.Trim();
        if (normalized.EndsWith('%'))
        {
            normalized = normalized[..^1].Trim();
        }

        return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out percent)
            && double.IsFinite(percent)
            && percent is >= 0 and <= 100;
    }

    public void MarkApplied(double percent)
    {
        _percent = percent;
        _originalPercent = percent;
        _text = Format(percent);
        _lastCommitAttempt = _text;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(HasUncommittedValue));
    }

    public void Revert()
    {
        _percent = _originalPercent;
        _text = Format(_originalPercent);
        _lastCommitAttempt = null;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(HasUncommittedValue));
    }

    private static string Format(double percent) =>
        percent.ToString("0.##", CultureInfo.InvariantCulture);
}
