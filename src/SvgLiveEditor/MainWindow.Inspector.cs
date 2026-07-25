using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private readonly SvgAttributeEditService _svgAttributeEditService = new();
    private readonly AvalonEditDocumentEditService _documentEditService = new();
    private readonly DispatcherTimer _inspectorCaretTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(160)
    };

    private bool _isSynchronizingInspectorSelection;
    private bool _isInspectorIndexCurrent;

    private void InitializeDocumentInspector()
    {
        _inspectorCaretTimer.Tick += OnInspectorCaretTimerTick;
    }

    private void DisposeDocumentInspector()
    {
        _inspectorCaretTimer.Stop();
        _inspectorCaretTimer.Tick -= OnInspectorCaretTimerTick;
    }

    private void ApplyDocumentInspectorResult(SvgDocumentIndexResult result)
    {
        ApplyDocumentInspectorResult(
            result,
            _viewModel.Inspector.CaptureSelectionIdentity());
    }

    private void ApplyDocumentInspectorResult(
        SvgDocumentIndexResult result,
        SvgElementIdentity? preferredSelection)
    {
        _isInspectorIndexCurrent = true;
        _isSynchronizingInspectorSelection = true;
        try
        {
            if (result.Document is SvgDocumentIndex document)
            {
                _viewModel.Inspector.Load(document, preferredSelection);
            }
            else
            {
                string message = result.Validation.IsValid
                    ? result.IndexError ?? "The current SVG could not be indexed."
                    : $"Current source is invalid: {result.Validation.Message}";
                _viewModel.Inspector.ShowUnavailable(message);
            }
        }
        finally
        {
            _isSynchronizingInspectorSelection = false;
        }
    }

    private void QueueInspectorCaretSynchronization()
    {
        if (_isSynchronizingInspectorSelection
            || !_isInspectorIndexCurrent
            || _viewModel.Inspector.DocumentIndex is null)
        {
            return;
        }

        _inspectorCaretTimer.Stop();
        _inspectorCaretTimer.Start();
    }

    private void MarkDocumentInspectorSourceChanged()
    {
        _isInspectorIndexCurrent = false;
        _inspectorCaretTimer.Stop();
    }

    private void OnInspectorCaretTimerTick(object? sender, EventArgs e)
    {
        _inspectorCaretTimer.Stop();
        SvgDocumentIndex? documentIndex = _viewModel.Inspector.DocumentIndex;
        if (documentIndex is null)
        {
            return;
        }

        SvgElementNode? element = documentIndex.FindElementAtOffset(
            SourceEditor.CaretOffset);
        if (element is null
            || _viewModel.Inspector.SelectedElement?.Element.StructuralPath
                .Equals(element.StructuralPath, StringComparison.Ordinal) == true)
        {
            return;
        }

        _isSynchronizingInspectorSelection = true;
        try
        {
            _viewModel.Inspector.SelectNode(element);
        }
        finally
        {
            _isSynchronizingInspectorSelection = false;
        }
    }

    private void OnInspectorTreeSelectionChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not SvgElementViewModel element)
        {
            return;
        }

        _viewModel.Inspector.SelectElement(element);
        if (_isSynchronizingInspectorSelection)
        {
            return;
        }

        SourceSpan span = element.Element.StartTagSpan;
        if (span.Start < 0
            || span.Length <= 0
            || span.Start > SourceEditor.Document.TextLength - span.Length)
        {
            return;
        }

        _isSynchronizingInspectorSelection = true;
        try
        {
            SourceEditor.Select(span.Start, span.Length);
            SourceEditor.ScrollToLine(
                SourceEditor.Document.GetLineByOffset(span.Start).LineNumber);
        }
        finally
        {
            _isSynchronizingInspectorSelection = false;
        }
    }

    private void OnInspectorPropertyLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { Tag: SvgPropertyViewModel property })
        {
            ApplyInspectorProperty(property);
        }
    }

    private void OnInspectorPropertyKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: SvgPropertyViewModel property })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            ApplyInspectorProperty(property);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            property.Revert();
            e.Handled = true;
        }
    }

    private void ApplyInspectorProperty(SvgPropertyViewModel property)
    {
        if (property.IsReadOnly)
        {
            return;
        }

        if (property.Value.Equals(
                property.OriginalValue,
                StringComparison.Ordinal))
        {
            property.ErrorMessage = string.Empty;
            return;
        }

        SvgAttributeEditResult result;
        try
        {
            result = _svgAttributeEditService.CreateEdit(
                SourceEditor.Text,
                property.Element,
                property.Name,
                property.Value);
        }
        catch (InvalidOperationException exception)
        {
            property.ErrorMessage = exception.Message;
            return;
        }

        if (!result.IsSuccess)
        {
            property.ErrorMessage =
                result.ErrorMessage ?? "The property value is invalid.";
            return;
        }

        if (result.Edit is null)
        {
            property.MarkApplied();
            return;
        }

        SvgElementIdentity preferredSelection = property.Element.Identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        property.MarkApplied();

        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, preferredSelection);
    }
}
