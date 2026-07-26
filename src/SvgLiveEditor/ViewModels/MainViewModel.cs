using System.IO;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private string _documentText = string.Empty;
    private string? _currentFilePath;
    private bool _isModified;
    private int _lineNumber = 1;
    private int _columnNumber = 1;
    private string _validationStatus = "Waiting for validation";
    private bool _isSvgValid;
    private string _operationStatus = string.Empty;
    private bool _canCopyPreviewAsPng;

    public string DocumentText => _documentText;

    public string? CurrentFilePath => _currentFilePath;

    public string CurrentFileName => string.IsNullOrWhiteSpace(_currentFilePath)
        ? "Untitled.svg"
        : Path.GetFileName(_currentFilePath);

    public bool IsModified => _isModified;

    public string SaveStatus => _isModified ? "Modified" : "Saved";

    public string WindowTitle => $"{CurrentFileName}{(_isModified ? " *" : string.Empty)} - SvgLiveEditor";

    public int LineNumber => _lineNumber;

    public int ColumnNumber => _columnNumber;

    public string ValidationStatus => _validationStatus;

    public bool IsSvgValid => _isSvgValid;

    public string OperationStatus => _operationStatus;

    public bool CanCopyPreviewAsPng => _canCopyPreviewAsPng;

    public DocumentInspectorViewModel Inspector { get; } = new();

    public void LoadDocument(string text, string? path)
    {
        _documentText = text;
        _currentFilePath = path;
        _isModified = false;
        _validationStatus = "Validating...";
        _isSvgValid = false;
        RaiseDocumentProperties();
        OnPropertyChanged(nameof(ValidationStatus));
        OnPropertyChanged(nameof(IsSvgValid));
    }

    public void UpdateTextFromEditor(string text)
    {
        if (_documentText.Equals(text, StringComparison.Ordinal))
        {
            return;
        }

        _documentText = text;
        _isModified = true;
        OnPropertyChanged(nameof(DocumentText));
        RaiseModifiedProperties();
    }

    public void MarkSaved(string path)
    {
        _currentFilePath = path;
        _isModified = false;
        RaiseDocumentProperties();
    }

    public void UpdateCaret(int lineNumber, int columnNumber)
    {
        SetProperty(ref _lineNumber, Math.Max(1, lineNumber), nameof(LineNumber));
        SetProperty(ref _columnNumber, Math.Max(1, columnNumber), nameof(ColumnNumber));
    }

    public void ApplyValidation(SvgValidationResult result)
    {
        _isSvgValid = result.IsValid;
        _validationStatus = result.IsValid
            ? result.Message
            : FormatValidationError(result);
        OnPropertyChanged(nameof(IsSvgValid));
        OnPropertyChanged(nameof(ValidationStatus));
    }

    public void SetOperationStatus(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        SetProperty(
            ref _operationStatus,
            message,
            nameof(OperationStatus));
    }

    public void SetCanCopyPreviewAsPng(bool canCopy)
    {
        SetProperty(
            ref _canCopyPreviewAsPng,
            canCopy,
            nameof(CanCopyPreviewAsPng));
    }

    private static string FormatValidationError(SvgValidationResult result)
    {
        if (result.LineNumber is null)
        {
            return $"Invalid: {result.Message}";
        }

        return $"Invalid at line {result.LineNumber}, column {result.ColumnNumber ?? 1}: {result.Message}";
    }

    private void RaiseDocumentProperties()
    {
        OnPropertyChanged(nameof(DocumentText));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(CurrentFileName));
        RaiseModifiedProperties();
    }

    private void RaiseModifiedProperties()
    {
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(SaveStatus));
        OnPropertyChanged(nameof(WindowTitle));
    }
}
