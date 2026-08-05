using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private const string FontCoverageWarning =
        "The selected font may not support every character; fallback fonts will be used.";

    private readonly SvgAttributeEditService _svgAttributeEditService = new();
    private readonly AvalonEditDocumentEditService _documentEditService = new();
    private readonly InspectorSourceGuard _inspectorSourceGuard = new();
    private readonly InspectorSelectionCoordinator _inspectorSelectionCoordinator = new();
    private readonly SvgFontFamilyStackService
        _svgFontFamilyStackService = new();
    private readonly InstalledFontGlyphCoverageService
        _installedFontGlyphCoverageService = new();
    private readonly SvgTextDirectionAdvisoryService
        _svgTextDirectionAdvisoryService = new();
    private readonly SvgLayerOrderService _svgLayerOrderService = new();
    private readonly SvgOpacityService _svgOpacityService = new();
    private readonly DispatcherTimer _inspectorCaretTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(160)
    };

    private bool _isSynchronizingInspectorSelection;
    private bool _isInspectorIndexCurrent;
    private bool _isEditorTextCompositionActive;
    private bool _isInspectorTextCompositionActive;
    private bool _isExplicitInspectorKeyboardNavigation;
    private long _inspectorSourceRevision = -1;
    private OpacitySliderGesture? _opacitySliderGesture;

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
        TextCompositionManager.AddPreviewTextInputStartHandler(
            InspectorPropertiesPanel,
            OnInspectorTextCompositionStarted);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(
            InspectorPropertiesPanel,
            OnInspectorTextCompositionUpdated);
        InspectorPropertiesPanel.PreviewTextInput +=
            OnInspectorTextCompositionCompleted;
        InspectorPropertiesPanel.LostKeyboardFocus +=
            OnInspectorPropertiesLostKeyboardFocus;
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
        TextCompositionManager.RemovePreviewTextInputStartHandler(
            InspectorPropertiesPanel,
            OnInspectorTextCompositionStarted);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(
            InspectorPropertiesPanel,
            OnInspectorTextCompositionUpdated);
        InspectorPropertiesPanel.PreviewTextInput -=
            OnInspectorTextCompositionCompleted;
        InspectorPropertiesPanel.LostKeyboardFocus -=
            OnInspectorPropertiesLostKeyboardFocus;
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
        CancelOpacitySliderGesture();
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
                    InspectorSelectionOrigin.InspectorRestore,
                    SourceEditor.Text);
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
        OnVisualInspectorResultApplied();
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
        CancelOpacitySliderGesture();
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
        SynchronizeVisualSelectionFromInspector();
        RefreshSelectedTextWarnings();
    }

    private void OnInspectorTreeSelectionChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not SvgElementViewModel element)
        {
            return;
        }

        CancelOpacitySliderGesture();
        InspectorSelectionOrigin origin =
            element.ConsumePendingSelectionOrigin()
            ?? (_isExplicitInspectorKeyboardNavigation
                ? InspectorSelectionOrigin.ExplicitTreeNavigation
                : InspectorSelectionOrigin.InspectorRestore);
        _isExplicitInspectorKeyboardNavigation = false;
        _viewModel.Inspector.AcceptTreeSelection(element);
        NavigateToInspectorElement(element, origin);
        SynchronizeVisualSelectionFromInspector(
            announce: origin
                == InspectorSelectionOrigin.ExplicitTreeNavigation);
        RefreshSelectedTextWarnings();
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

    private void OnArrangeMenuSubmenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        UpdateArrangeMenuItem(BringToFrontMenuItem, SvgLayerOrderCommand.BringToFront);
        UpdateArrangeMenuItem(BringForwardMenuItem, SvgLayerOrderCommand.BringForward);
        UpdateArrangeMenuItem(SendBackwardMenuItem, SvgLayerOrderCommand.SendBackward);
        UpdateArrangeMenuItem(SendToBackMenuItem, SvgLayerOrderCommand.SendToBack);
    }

    private void OnInspectorArrangeContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        if (InspectorTree.ContextMenu is not ContextMenu contextMenu)
        {
            return;
        }

        foreach (MenuItem item in contextMenu.Items.OfType<MenuItem>())
        {
            if (TryReadLayerOrderCommand(item.Tag, out SvgLayerOrderCommand command))
            {
                UpdateArrangeMenuItem(item, command);
            }
        }
    }

    private void OnArrangeClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item
            && TryReadLayerOrderCommand(item.Tag, out SvgLayerOrderCommand command))
        {
            ApplyLayerOrder(command);
        }
    }

    private void ApplyLayerOrder(SvgLayerOrderCommand command)
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        SvgElementNode? element =
            _viewModel.Inspector.SelectedElement?.Element;
        long expectedRevision = _inspectorSourceRevision;
        if (document is null
            || element is null
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                expectedRevision,
                _sourceRevisionTracker.Current,
                isEditorTextCompositionActive: false))
        {
            _viewModel.SetOperationStatus(
                "Arrange is unavailable until the current SVG is valid and selected.");
            return;
        }

        SvgLayerOrderAvailability availability =
            GetLayerOrderAvailability(command);
        if (!availability.CanExecute)
        {
            _viewModel.SetOperationStatus(
                availability.UnavailableReason
                ?? "The selected element cannot be reordered.");
            return;
        }

        CancelOpacitySliderGesture();
        CancelVisualEditGesture();
        string sourceSnapshot = SourceEditor.Text;
        SvgLayerOrderEditResult result = _svgLayerOrderService.CreateEdit(
            sourceSnapshot,
            document,
            element,
            command);
        if (!result.IsSuccess || result.Edit is null
            || result.PreferredSelection is null)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage ?? "The selected element is already at that boundary.");
            return;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; select the element again.");
            return;
        }

        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, result.PreferredSelection);
        _viewModel.SetOperationStatus(GetLayerOrderStatus(command));
    }

    private void UpdateArrangeMenuItem(
        MenuItem item,
        SvgLayerOrderCommand command)
    {
        SvgLayerOrderAvailability availability = GetLayerOrderAvailability(command);
        item.IsEnabled = availability.CanExecute;
        item.ToolTip = availability.UnavailableReason;
    }

    private SvgLayerOrderAvailability GetLayerOrderAvailability(
        SvgLayerOrderCommand command)
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        SvgElementNode? element =
            _viewModel.Inspector.SelectedElement?.Element;
        if (document is null
            || element is null
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive))
        {
            return new SvgLayerOrderAvailability(
                false,
                "Select an eligible element in a valid current SVG.");
        }

        SvgVisualElement? visualElement =
            _lastValidVisualDocument?.FindElement(element.Identity);
        if (visualElement is null || !visualElement.IsSelectable)
        {
            return new SvgLayerOrderAvailability(
                false,
                visualElement?.UnsupportedReason
                    ?? "Arrange requires reliably bounded visible artwork.");
        }

        return _svgLayerOrderService.GetAvailability(document, element, command);
    }

    private static bool TryReadLayerOrderCommand(
        object? value,
        out SvgLayerOrderCommand command)
    {
        command = default;
        return value is string text
            && Enum.TryParse(text, ignoreCase: false, out command);
    }

    private static string GetLayerOrderStatus(SvgLayerOrderCommand command) =>
        command switch
        {
            SvgLayerOrderCommand.BringToFront => "Element brought to front",
            SvgLayerOrderCommand.BringForward => "Element brought forward",
            SvgLayerOrderCommand.SendBackward => "Element sent backward",
            SvgLayerOrderCommand.SendToBack => "Element sent to back",
            _ => "Element reordered"
        };

    private void OnOpacityTextLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SvgOpacityViewModel opacity })
        {
            _ = ApplyOpacity(opacity);
        }
    }

    private void OnOpacityTextKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SvgOpacityViewModel opacity })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = ApplyOpacity(opacity);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            opacity.Revert();
            e.Handled = true;
        }
    }

    private void OnOpacitySliderPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_viewModel.Inspector.Opacity is not SvgOpacityViewModel
            {
                IsEnabled: true
            } opacity)
        {
            return;
        }

        CancelVisualEditGesture();
        _opacitySliderGesture = new OpacitySliderGesture(
            opacity,
            _inspectorSourceRevision,
            opacity.Element.Identity);
    }

    private void OnOpacitySliderPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e) =>
        CompleteOpacitySliderGesture();

    private void OnOpacitySliderLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (_opacitySliderGesture is not null)
        {
            CancelOpacitySliderGesture();
        }
    }

    private void OnOpacitySliderPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelOpacitySliderGesture();
            _viewModel.Inspector.Opacity?.Revert();
            e.Handled = true;
        }
    }

    private void OnOpacitySliderPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.Home or Key.End or Key.PageUp or Key.PageDown
            && _viewModel.Inspector.Opacity is SvgOpacityViewModel opacity)
        {
            _ = ApplyOpacity(opacity);
        }
    }

    private void CompleteOpacitySliderGesture()
    {
        OpacitySliderGesture? gesture = _opacitySliderGesture;
        _opacitySliderGesture = null;
        if (gesture is null
            || _viewModel.Inspector.Opacity != gesture.Opacity
            || !_sourceRevisionTracker.IsCurrent(gesture.SourceRevision)
            || gesture.Opacity.Element.Identity != gesture.ElementIdentity)
        {
            gesture?.Opacity.Revert();
            return;
        }

        _ = ApplyOpacity(gesture.Opacity);
    }

    private void CancelOpacitySliderGesture()
    {
        OpacitySliderGesture? gesture = _opacitySliderGesture;
        _opacitySliderGesture = null;
        gesture?.Opacity.Revert();
    }

    private bool ApplyOpacity(SvgOpacityViewModel opacity)
    {
        if (!ReferenceEquals(_viewModel.Inspector.Opacity, opacity)
            || !opacity.IsEnabled)
        {
            return false;
        }
        if (opacity.WasCurrentTextAlreadyAttempted)
        {
            return false;
        }
        opacity.MarkCommitAttempt();
        if (!opacity.TryReadPercent(out double percent))
        {
            opacity.ErrorMessage =
                "Enter a percentage from 0 to 100 using invariant digits.";
            return false;
        }

        long expectedRevision = _inspectorSourceRevision;
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        if (document is null
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                expectedRevision,
                _sourceRevisionTracker.Current,
                isEditorTextCompositionActive: false))
        {
            opacity.ErrorMessage =
                "The source changed; select the element again.";
            return false;
        }

        string sourceSnapshot = SourceEditor.Text;
        SvgAttributeEditResult result = _svgOpacityService.CreateEdit(
            sourceSnapshot,
            document,
            opacity.Element,
            percent);
        if (!result.IsSuccess)
        {
            opacity.ErrorMessage =
                result.ErrorMessage ?? "Opacity could not be changed.";
            return false;
        }
        if (result.Edit is null)
        {
            opacity.MarkApplied(percent);
            return true;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            opacity.ErrorMessage =
                "The source changed; select the element again.";
            return false;
        }

        SvgElementIdentity preferredSelection = opacity.Element.Identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, preferredSelection);
        _viewModel.SetOperationStatus($"Opacity set to {percent:0.##}%");
        return true;
    }

    private sealed record OpacitySliderGesture(
        SvgOpacityViewModel Opacity,
        long SourceRevision,
        SvgElementIdentity ElementIdentity);

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
            _ = ApplyInspectorProperty(property);
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
            _ = ApplyInspectorProperty(property);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            property.Revert();
            e.Handled = true;
        }
    }

    private bool TryHandleInspectorUndoShortcut(Key pressedKey)
    {
        InspectorUndoShortcut shortcut = pressedKey switch
        {
            Key.Z => InspectorUndoShortcut.Undo,
            Key.Y => InspectorUndoShortcut.Redo,
            _ => throw new ArgumentOutOfRangeException(nameof(pressedKey))
        };
        object? editContext = FindInspectorEditContext(
            Keyboard.FocusedElement as DependencyObject);
        bool hasUncommittedValue = editContext switch
        {
            SvgPropertyViewModel property => property.HasUncommittedValue,
            SvgOpacityViewModel opacity => opacity.HasUncommittedValue,
            _ => false
        };
        bool hasLocalRedo = Keyboard.FocusedElement is TextBoxBase
        {
            CanRedo: true
        };
        InspectorUndoShortcutRoute route =
            InspectorUndoShortcutRouter.Resolve(
                shortcut,
                new InspectorUndoFocusState(
                    SourceEditor.IsKeyboardFocusWithin,
                    InspectorPropertiesPanel.IsKeyboardFocusWithin,
                    hasUncommittedValue,
                    hasLocalRedo,
                    _isInspectorTextCompositionActive));

        if (route == InspectorUndoShortcutRoute.DocumentUndo)
        {
            OnUndoClick(this, new RoutedEventArgs());
            return true;
        }
        if (route == InspectorUndoShortcutRoute.DocumentRedo)
        {
            OnRedoClick(this, new RoutedEventArgs());
            return true;
        }

        return false;
    }

    private static object? FindInspectorEditContext(DependencyObject? element)
    {
        for (DependencyObject? current = element;
             current is not null;
             current = GetVisualOrLogicalParent(current))
        {
            if (current is FrameworkElement
                {
                    Tag: SvgPropertyViewModel or SvgOpacityViewModel
                } tagged)
            {
                return tagged.Tag;
            }
        }

        return null;
    }

    private void OnInspectorHelpGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { ToolTip: ToolTip toolTip } element)
        {
            toolTip.PlacementTarget = element;
            toolTip.Placement = PlacementMode.Right;
            toolTip.IsOpen = true;
        }
    }

    private void OnInspectorHelpLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { ToolTip: ToolTip toolTip })
        {
            toolTip.IsOpen = false;
        }
    }

    private void OnInspectorSuggestedPropertySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox
            {
                Tag: SvgPropertyViewModel property
            } comboBox
            || !property.Name.Equals(
                "font-family",
                StringComparison.Ordinal)
            || e.AddedItems.Count != 1
            || e.AddedItems[0] is not string selectedFamily
            || !comboBox.IsDropDownOpen)
        {
            return;
        }

        property.Value = selectedFamily;
        _ = ApplyInspectorProperty(property);
        e.Handled = true;
    }

    private bool ApplyInspectorProperty(SvgPropertyViewModel property)
    {
        if (property.IsReadOnly)
        {
            return false;
        }

        if (property.Value.Equals(
                property.OriginalValue,
                StringComparison.Ordinal))
        {
            property.ErrorMessage = string.Empty;
            return true;
        }
        if (property.WasCurrentValueAlreadyAttempted)
        {
            return false;
        }
        property.MarkCommitAttempt();

        string commitValue = property.Value;
        if (property.Definition.UsesFontFamilySuggestions
            && !_svgFontFamilyStackService.TryCreateForPrimary(
                property.SerializedValue,
                property.Value,
                out commitValue))
        {
            property.ErrorMessage =
                "Enter one safe local font-family name.";
            return false;
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
            return false;
        }

        string sourceSnapshot = SourceEditor.Text;
        SvgAttributeEditResult result;
        try
        {
            result = _svgAttributeEditService.CreateEdit(
                sourceSnapshot,
                property.Element,
                property.Name,
                commitValue);
        }
        catch (InvalidOperationException exception)
        {
            property.ErrorMessage = exception.Message;
            return false;
        }

        if (!result.IsSuccess)
        {
            property.ErrorMessage =
                result.ErrorMessage ?? "The property value is invalid.";
            return false;
        }

        if (result.Edit is null)
        {
            property.MarkApplied(commitValue);
            return true;
        }

        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            property.ErrorMessage =
                "The source changed; select the element again.";
            return false;
        }

        SvgElementIdentity preferredSelection = property.Element.Identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        property.MarkApplied(commitValue);

        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, preferredSelection);
        return true;
    }

    private void RefreshSelectedTextWarnings()
    {
        SvgVisualTextMeasurementSpec? selectedMeasurement =
            _viewModel.Inspector.SelectedElement is SvgElementViewModel selected
                ? _lastValidVisualDocument?
                    .FindElement(selected.Element.Identity)?
                    .TextMeasurement
                : null;
        string? directionWarning = selectedMeasurement is null
            ? null
            : _svgTextDirectionAdvisoryService.GetWarning(
                selectedMeasurement.Text,
                selectedMeasurement.Direction);
        _viewModel.Inspector.SetSelectionAdvisory(directionWarning);
        if (directionWarning is not null)
        {
            _viewModel.SetOperationStatus(directionWarning);
            return;
        }

        SvgPropertyViewModel? property = _viewModel.Inspector.Properties
            .FirstOrDefault(item => item.Definition.UsesFontFamilySuggestions);
        string? selectedText = selectedMeasurement?.Text;
        FontGlyphCoverage coverage = property is null
            || string.IsNullOrEmpty(selectedText)
                ? FontGlyphCoverage.Unknown
                : _installedFontGlyphCoverageService.Check(
                    property.Value,
                    selectedText);
        if (coverage == FontGlyphCoverage.Incomplete)
        {
            _viewModel.SetOperationStatus(FontCoverageWarning);
        }
        else if (IsSelectedTextWarning(_viewModel.OperationStatus))
        {
            _viewModel.SetOperationStatus("Ready");
        }
    }

    private static bool IsSelectedTextWarning(string status) =>
        status.Equals(FontCoverageWarning, StringComparison.Ordinal)
        || status.Equals(
            SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection,
            StringComparison.Ordinal)
        || status.Equals(
            SvgTextDirectionAdvisoryService.LtrTextWithRtlDirection,
            StringComparison.Ordinal);

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

    private void OnInspectorTextCompositionStarted(
        object sender,
        TextCompositionEventArgs e)
    {
        _isInspectorTextCompositionActive = true;
    }

    private void OnInspectorTextCompositionUpdated(
        object sender,
        TextCompositionEventArgs e)
    {
        _isInspectorTextCompositionActive = true;
    }

    private void OnInspectorTextCompositionCompleted(
        object sender,
        TextCompositionEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => _isInspectorTextCompositionActive = false));
    }

    private void OnInspectorPropertiesLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!InspectorPropertiesPanel.IsKeyboardFocusWithin)
                {
                    _isInspectorTextCompositionActive = false;
                }
            }));
    }
}
