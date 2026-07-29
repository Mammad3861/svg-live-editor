using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private readonly SvgAttributeEditService _svgAttributeEditService = new();
    private readonly AvalonEditDocumentEditService _documentEditService = new();
    private readonly InspectorSourceGuard _inspectorSourceGuard = new();
    private readonly InspectorSelectionCoordinator _inspectorSelectionCoordinator = new();
    private readonly DispatcherTimer _inspectorCaretTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(160)
    };

    private bool _isSynchronizingInspectorSelection;
    private bool _isInspectorIndexCurrent;
    private bool _isEditorTextCompositionActive;
    private bool _isExplicitInspectorKeyboardNavigation;
    private long _inspectorSourceRevision = -1;

    private void InitializeDocumentInspector()
    {
        _inspectorCaretTimer.Tick += OnInspectorCaretTimerTick;
        TextCompositionManager.AddPreviewTextInputStartHandler(
            SourceEditor,
            OnEditorTextCompositionStarted);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(
            SourceEditor,
            OnEditorTextCompositionUpdated);
        SourceEditor.PreviewTextInput += OnEditorTextCompositionCompleted;
        SourceEditor.LostKeyboardFocus += OnEditorLostKeyboardFocus;
    }

    private void DisposeDocumentInspector()
    {
        _inspectorCaretTimer.Stop();
        _inspectorCaretTimer.Tick -= OnInspectorCaretTimerTick;
        TextCompositionManager.RemovePreviewTextInputStartHandler(
            SourceEditor,
            OnEditorTextCompositionStarted);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(
            SourceEditor,
            OnEditorTextCompositionUpdated);
        SourceEditor.PreviewTextInput -= OnEditorTextCompositionCompleted;
        SourceEditor.LostKeyboardFocus -= OnEditorLostKeyboardFocus;
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
        _inspectorSourceRevision = _sourceRevisionTracker.Current;
        _isSynchronizingInspectorSelection = true;
        try
        {
            if (result.Document is SvgDocumentIndex document)
            {
                _viewModel.Inspector.Load(
                    document,
                    preferredSelection,
                    InspectorSelectionOrigin.InspectorRestore);
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
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive)
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
        _inspectorSourceRevision = -1;
        _inspectorCaretTimer.Stop();
    }

    private void OnInspectorCaretTimerTick(object? sender, EventArgs e)
    {
        _inspectorCaretTimer.Stop();
        SvgDocumentIndex? documentIndex = _viewModel.Inspector.DocumentIndex;
        if (!_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive)
            || documentIndex is null)
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
            _viewModel.Inspector.SelectNode(
                element,
                InspectorSelectionOrigin.SourceCaretSync);
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

        InspectorSelectionOrigin origin =
            element.ConsumePendingSelectionOrigin()
            ?? (_isExplicitInspectorKeyboardNavigation
                ? InspectorSelectionOrigin.ExplicitTreeNavigation
                : InspectorSelectionOrigin.InspectorRestore);
        _isExplicitInspectorKeyboardNavigation = false;
        _viewModel.Inspector.AcceptTreeSelection(element);
        NavigateToInspectorElement(element, origin);
    }

    private void OnInspectorTreePreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject originalSource
            || FindNearestTreeElement(originalSource)
                is not SvgElementViewModel element)
        {
            return;
        }

        NavigateToInspectorElement(
            element,
            InspectorSelectionOrigin.ExplicitTreeNavigation);
    }

    private static SvgElementViewModel? FindNearestTreeElement(
        DependencyObject originalSource)
    {
        for (DependencyObject? current = originalSource;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is TreeViewItem
                {
                    DataContext: SvgElementViewModel element
                })
            {
                return element;
            }

            if (current is TreeView)
            {
                break;
            }
        }

        return null;
    }

    private void OnInspectorTreePreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Enter or Key.Space)
        {
            if (InspectorTree.SelectedItem is SvgElementViewModel element)
            {
                NavigateToInspectorElement(
                    element,
                    InspectorSelectionOrigin.ExplicitTreeNavigation);
                e.Handled = true;
            }
            return;
        }

        _isExplicitInspectorKeyboardNavigation = key is
            Key.Up or Key.Down or Key.Left or Key.Right
            or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    }

    private void OnInspectorTreePreviewKeyUp(
        object sender,
        KeyEventArgs e)
    {
        _isExplicitInspectorKeyboardNavigation = false;
    }

    private void OnInspectorTreeLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        _isExplicitInspectorKeyboardNavigation = false;
    }

    private void NavigateToInspectorElement(
        SvgElementViewModel element,
        InspectorSelectionOrigin origin)
    {
        if (!_inspectorSelectionCoordinator.TryGetNavigationSpan(
                origin,
                element.Element.StartTagSpan,
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive,
                SourceEditor.Document.TextLength,
                out SourceSpan span))
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
        if (sender is FrameworkElement { Tag: SvgPropertyViewModel property })
        {
            ApplyInspectorProperty(property);
        }
    }

    private void OnInspectorPropertyKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SvgPropertyViewModel property })
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

        long expectedRevision = _inspectorSourceRevision;
        if (!_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                expectedRevision,
                _sourceRevisionTracker.Current,
                isEditorTextCompositionActive: false))
        {
            property.ErrorMessage =
                "The source changed; select the element again.";
            return;
        }

        string sourceSnapshot = SourceEditor.Text;
        SvgAttributeEditResult result;
        try
        {
            result = _svgAttributeEditService.CreateEdit(
                sourceSnapshot,
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

        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            property.ErrorMessage =
                "The source changed; select the element again.";
            return;
        }

        SvgElementIdentity preferredSelection = property.Element.Identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        property.MarkApplied();

        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, preferredSelection);
    }

    private void OnEditorTextCompositionStarted(
        object sender,
        TextCompositionEventArgs e)
    {
        _isEditorTextCompositionActive = true;
        _inspectorCaretTimer.Stop();
    }

    private void OnEditorTextCompositionUpdated(
        object sender,
        TextCompositionEventArgs e)
    {
        _isEditorTextCompositionActive = true;
        _inspectorCaretTimer.Stop();
    }

    private void OnEditorTextCompositionCompleted(
        object sender,
        TextCompositionEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                _isEditorTextCompositionActive = false;
                QueueInspectorCaretSynchronization();
            }));
    }

    private void OnEditorLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!SourceEditor.IsKeyboardFocusWithin)
                {
                    _isEditorTextCompositionActive = false;
                }
            }));
    }
}
