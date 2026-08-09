using System.Windows;
using System.Windows.Automation;
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
    private readonly SvgLayerVisibilityService _svgLayerVisibilityService = new();
    private readonly SvgOpacityService _svgOpacityService = new();
    private readonly SvgElementCreationService _svgElementCreationService = new();
    private readonly SvgElementDuplicateService _svgElementDuplicateService = new();
    private readonly SvgElementDeleteService _svgElementDeleteService = new();
    private readonly SvgLayerRenameService _svgLayerRenameService = new();
    private readonly SvgLayerReparentService _svgLayerReparentService = new();
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
    private SvgLayerViewModel? _layerDragCandidate;
    private SvgLayerViewModel? _layerDropTarget;
    private Point _layerDragStart;
    private long _layerDragSourceRevision = -1;
    private SvgLayerDropPlacement _layerDropPlacement;

    private const string LayerDragDataFormat =
        "SvgLiveEditor.Internal.Layer.OpaqueId";

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
        TextCompositionManager.AddPreviewTextInputStartHandler(
            LayersTree,
            OnInspectorTextCompositionStarted);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(
            LayersTree,
            OnInspectorTextCompositionUpdated);
        LayersTree.PreviewTextInput += OnInspectorTextCompositionCompleted;
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
        TextCompositionManager.RemovePreviewTextInputStartHandler(
            LayersTree,
            OnInspectorTextCompositionStarted);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(
            LayersTree,
            OnInspectorTextCompositionUpdated);
        LayersTree.PreviewTextInput -= OnInspectorTextCompositionCompleted;
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
                    SourceEditor.Text,
                    _lastValidVisualSourceRevision
                        == _sourceRevisionTracker.Current
                        ? _lastValidVisualDocument
                        : null);
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
        RefreshAuthoringControls();
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
        RefreshAuthoringControls();
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
        RefreshAuthoringControls();
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
        RefreshAuthoringControls();
    }

    private void OnLayersTreeSelectionChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not SvgLayerViewModel layer)
        {
            return;
        }

        CancelOpacitySliderGesture();
        InspectorSelectionOrigin origin =
            layer.ConsumePendingSelectionOrigin()
            ?? (_isExplicitInspectorKeyboardNavigation
                ? InspectorSelectionOrigin.ExplicitTreeNavigation
                : InspectorSelectionOrigin.InspectorRestore);
        _isExplicitInspectorKeyboardNavigation = false;
        _viewModel.Inspector.AcceptLayerSelection(layer);
        if (origin == InspectorSelectionOrigin.ExplicitTreeNavigation
            && _viewModel.Inspector.FindViewModel(layer.Element)
                is SvgElementViewModel structureElement)
        {
            NavigateToInspectorElement(structureElement, origin);
        }
        SynchronizeVisualSelectionFromInspector(
            announce: origin
                == InspectorSelectionOrigin.ExplicitTreeNavigation);
        RefreshSelectedTextWarnings();
        RefreshAuthoringControls();
    }

    private void OnLayersTreePreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (e.OriginalSource is DependencyObject originalSource
            && FindVisualAncestor<TextBox>(originalSource)
                is TextBox { Tag: SvgLayerViewModel renameLayer } renameTextBox
            && key is Key.Enter or Key.Escape)
        {
            // PreviewKeyDown reaches the TreeView before the inline TextBox's
            // bubbling KeyDown. Resolve rename keys here so the tree's Enter
            // navigation behavior cannot consume the commit first.
            if (TryHandleLayerRenameKey(renameTextBox, renameLayer, key))
            {
                e.Handled = true;
            }
            return;
        }

        if (key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            BeginSelectedLayerRename();
            e.Handled = true;
            return;
        }
        if (key is Key.Enter or Key.Space)
        {
            if (LayersTree.SelectedItem is SvgLayerViewModel layer
                && _viewModel.Inspector.FindViewModel(layer.Element)
                    is SvgElementViewModel structureElement)
            {
                _viewModel.Inspector.AcceptLayerSelection(layer);
                NavigateToInspectorElement(
                    structureElement,
                    InspectorSelectionOrigin.ExplicitTreeNavigation);
                SynchronizeVisualSelectionFromInspector(announce: true);
                e.Handled = true;
            }
            return;
        }

        _isExplicitInspectorKeyboardNavigation = key is
            Key.Up or Key.Down or Key.Left or Key.Right
            or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    }

    private void OnLayersTreePreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        ClearLayerDropTarget();
        _layerDragCandidate = null;
        if (e.OriginalSource is not DependencyObject originalSource
            || FindVisualAncestor<ButtonBase>(originalSource) is not null
            || FindNearestLayer(originalSource)
                is not SvgLayerViewModel layer)
        {
            return;
        }

        _layerDragCandidate = layer;
        _layerDragStart = e.GetPosition(LayersTree);
        _layerDragSourceRevision = _sourceRevisionTracker.Current;
    }

    private void OnLayersTreePreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_layerDragCandidate is not SvgLayerViewModel layer
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(LayersTree);
        if (Math.Abs(current.X - _layerDragStart.X)
                < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _layerDragStart.Y)
                < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _layerDragCandidate = null;
        DataObject data = new();
        data.SetData(LayerDragDataFormat, layer.OpaqueId);
        try
        {
            DragDrop.DoDragDrop(LayersTree, data, DragDropEffects.Move);
        }
        finally
        {
            ClearLayerDropTarget();
            _layerDragSourceRevision = -1;
        }
    }

    private void OnLayersTreeDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        string? reason = null;
        if (!TryGetLayerDragPair(e, out SvgLayerViewModel source, out SvgLayerViewModel target))
        {
            ClearLayerDropTarget();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (!_viewModel.OperationStatus.Equals(
                        reason,
                        StringComparison.Ordinal))
                {
                    _viewModel.SetOperationStatus(reason);
                }
                e.Effects = DragDropEffects.None;
            }
            return;
        }

        TreeViewItem? container = FindVisualAncestor<TreeViewItem>(
            (DependencyObject)e.OriginalSource);
        if (container is null)
        {
            ClearLayerDropTarget();
            return;
        }
        Point position = e.GetPosition(container);
        double height = Math.Max(1, container.ActualHeight);
        SvgLayerDropPlacement placement = target.IsGroup
            && position.Y >= height * 0.3
            && position.Y <= height * 0.7
                ? SvgLayerDropPlacement.Inside
                : position.Y < height / 2
                    ? SvgLayerDropPlacement.Before
                    : SvgLayerDropPlacement.After;
        if (!CanDropLayer(source, target, placement, out reason))
        {
            ClearLayerDropTarget();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _viewModel.SetOperationStatus(reason);
            }
            return;
        }
        SetLayerDropTarget(target, placement);
        e.Effects = DragDropEffects.Move;
    }

    private void OnLayersTreeDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        string? reason = null;
        if (!TryGetLayerDragPair(e, out SvgLayerViewModel source, out SvgLayerViewModel target))
        {
            ClearLayerDropTarget();
            _viewModel.SetOperationStatus(
                reason ?? "The layer drop was rejected.");
            return;
        }

        SvgLayerDropPlacement placement = ReferenceEquals(
            target,
            _layerDropTarget)
                ? _layerDropPlacement
                : SvgLayerDropPlacement.Before;
        if (!CanDropLayer(source, target, placement, out reason))
        {
            ClearLayerDropTarget();
            _viewModel.SetOperationStatus(
                reason ?? "The layer drop was rejected.");
            return;
        }
        ClearLayerDropTarget();
        ApplyLayerMove(source, target, placement);
    }

    private void OnLayersTreeDragLeave(object sender, DragEventArgs e)
    {
        if (!LayersTree.IsMouseOver)
        {
            ClearLayerDropTarget();
        }
    }

    private void OnLayerVisibilityClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SvgLayerViewModel layer })
        {
            ApplyLayerVisibility(layer);
            e.Handled = true;
        }
    }

    private void OnLayerLockClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SvgLayerViewModel layer })
        {
            return;
        }

        CancelOpacitySliderGesture();
        CancelVisualEditGesture();
        if (_viewModel.Inspector.ToggleLayerLock(layer))
        {
            ShowVisualSelection();
            _viewModel.SetOperationStatus(
                layer.IsLocked
                    ? $"{layer.Label} unlocked for this session"
                    : $"{layer.Label} locked for this session");
            RefreshAuthoringControls();
        }
        e.Handled = true;
    }

    private void ApplyLayerVisibility(SvgLayerViewModel layer)
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        long expectedRevision = _inspectorSourceRevision;
        if (document is null
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                expectedRevision,
                _sourceRevisionTracker.Current,
                isEditorTextCompositionActive: false))
        {
            _viewModel.SetOperationStatus(
                "Visibility is unavailable until the current SVG is valid.");
            return;
        }

        string sourceSnapshot = SourceEditor.Text;
        SvgLayerVisibilityEditResult result =
            _svgLayerVisibilityService.CreateEdit(
                sourceSnapshot,
                document,
                layer.Element,
                _viewModel.Inspector.IsHiddenAttributeOwned(layer));
        if (!result.IsSuccess || result.Edit is null)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage
                ?? "Visibility is already at the requested state.");
            return;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; select the layer again.");
            return;
        }

        CancelOpacitySliderGesture();
        CancelVisualEditGesture();
        string opaqueId = layer.OpaqueId;
        SvgElementIdentity selection = layer.Element.Identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        _viewModel.Inspector.SetHiddenAttributeOwned(
            opaqueId,
            result.OwnsHiddenAttributeAfterEdit);
        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(SourceEditor.Text);
        ApplyDocumentInspectorResult(rebuilt, selection);
        _viewModel.SetOperationStatus(
            result.OwnsHiddenAttributeAfterEdit
                ? $"{layer.Label} hidden"
                : $"{layer.Label} shown");
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

    private static SvgLayerViewModel? FindNearestLayer(
        DependencyObject originalSource)
    {
        TreeViewItem? item = FindVisualAncestor<TreeViewItem>(originalSource);
        return item?.DataContext as SvgLayerViewModel;
    }

    private static T? FindVisualAncestor<T>(DependencyObject originalSource)
        where T : DependencyObject
    {
        for (DependencyObject? current = originalSource;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private bool TryGetLayerDragPair(
        DragEventArgs e,
        out SvgLayerViewModel source,
        out SvgLayerViewModel target)
    {
        source = null!;
        target = null!;
        if (!e.Data.GetDataPresent(LayerDragDataFormat)
            || e.Data.GetData(LayerDragDataFormat) is not string opaqueId
            || e.OriginalSource is not DependencyObject originalSource
            || _viewModel.Inspector.FindLayerViewModel(opaqueId)
                is not SvgLayerViewModel sourceLayer
            || FindNearestLayer(originalSource)
                is not SvgLayerViewModel targetLayer)
        {
            return false;
        }

        source = sourceLayer;
        target = targetLayer;
        return true;
    }

    private bool CanDropLayer(
        SvgLayerViewModel source,
        SvgLayerViewModel target,
        SvgLayerDropPlacement placement,
        out string? reason)
    {
        reason = null;
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        if (document is null
            || !_sourceRevisionTracker.IsCurrent(_layerDragSourceRevision)
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _layerDragSourceRevision,
                _sourceRevisionTracker.Current,
                isEditorTextCompositionActive: false))
        {
            reason = "The source changed; start the layer drag again.";
            return false;
        }
        SvgAuthoringAvailability availability =
            _svgLayerReparentService.GetDropAvailability(
                SourceEditor.Text,
                document,
                source.Element,
                target.Element,
                placement,
                _viewModel.Inspector.IsElementEffectivelyLocked);
        reason = availability.UnavailableReason;
        return availability.CanExecute;
    }

    private void SetLayerDropTarget(
        SvgLayerViewModel target,
        SvgLayerDropPlacement placement)
    {
        if (ReferenceEquals(_layerDropTarget, target)
            && _layerDropPlacement == placement)
        {
            return;
        }

        ClearLayerDropTarget();
        _layerDropTarget = target;
        _layerDropPlacement = placement;
        target.IsDropBefore = placement == SvgLayerDropPlacement.Before;
        target.IsDropAfter = placement == SvgLayerDropPlacement.After;
        target.IsDropInside = placement == SvgLayerDropPlacement.Inside;
    }

    private void ClearLayerDropTarget()
    {
        if (_layerDropTarget is not null)
        {
            _layerDropTarget.IsDropBefore = false;
            _layerDropTarget.IsDropAfter = false;
            _layerDropTarget.IsDropInside = false;
        }

        _layerDropTarget = null;
    }

    private void ApplyLayerMove(
        SvgLayerViewModel source,
        SvgLayerViewModel target,
        SvgLayerDropPlacement placement)
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        long expectedRevision = _layerDragSourceRevision;
        string sourceSnapshot = SourceEditor.Text;
        if (document is null
            || !_sourceRevisionTracker.IsCurrent(expectedRevision))
        {
            _viewModel.SetOperationStatus(
                "The source changed; start the layer drag again.");
            return;
        }

        SvgAuthoringEditResult result = _svgLayerReparentService.CreateDropEdit(
            sourceSnapshot,
            document,
            source.Element,
            target.Element,
            placement,
            _viewModel.Inspector.IsElementEffectivelyLocked);
        if (!result.IsSuccess
            || result.Edit is null
            || result.PreferredSelection is null)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage ?? "The layer could not be reordered.");
            return;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; start the layer drag again.");
            return;
        }

        CancelOpacitySliderGesture();
        CancelVisualEditGesture();
        string destination = placement == SvgLayerDropPlacement.Inside
            ? target.Label
            : target.Parent?.Label ?? "the SVG root";
        ApplyAuthoringEdit(
            result,
            sourceSnapshot,
            expectedRevision,
            ReferenceEquals(source.Parent, target.Parent)
                && placement != SvgLayerDropPlacement.Inside
                    ? $"{source.Label} reordered within {destination}"
                    : $"{source.Label} moved into {destination}");
    }

    private void OnAddElementClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: ContextMenu menu } button)
        {
            return;
        }

        UpdateAuthoringMenuItems(menu.Items);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnAddElementContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            UpdateAuthoringMenuItems(menu.Items);
        }
    }

    private void OnEditMenuSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menu)
        {
            UpdateAuthoringMenuItems(menu.Items);
        }
    }

    private void OnCreateElementClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item
            && TryReadCreateElementCommand(
                item.Tag,
                out SvgCreateDestination destination,
                out SvgCreateElementKind kind))
        {
            CreateVisualElement(destination, kind);
        }
    }

    private void OnDuplicateElementClick(object sender, RoutedEventArgs e) =>
        DuplicateSelectedElement();

    private void OnDeleteElementClick(object sender, RoutedEventArgs e) =>
        DeleteSelectedElement();

    private void OnRenameLayerClick(object sender, RoutedEventArgs e)
    {
        InspectorModeTabs.SelectedItem = LayersTab;
        BeginSelectedLayerRename();
    }

    private void OnMoveElementToRootClick(object sender, RoutedEventArgs e) =>
        MoveSelectedElementToRoot();

    private void CreateVisualElement(
        SvgCreateDestination destination,
        SvgCreateElementKind kind)
    {
        if (!TryGetAuthoringContext(
                out SvgDocumentIndex document,
                out SvgElementNode? selection,
                out string source,
                out long revision,
                "Create"))
        {
            return;
        }

        SvgAuthoringEditResult result = _svgElementCreationService.CreateEdit(
            source,
            document,
            selection,
            destination,
            kind,
            _lastValidCanvasSize ?? new SvgCanvasSize(300, 150),
            _viewModel.Inspector.IsElementEffectivelyLocked);
        SvgElementNode? parent = SvgElementCreationService.ResolveInsertionParent(
            document,
            selection,
            destination);
        string destinationLabel = parent?.Name == "svg"
            ? "SVG root"
            : parent?.DisplayLabel ?? "selected context";
        ApplyAuthoringEdit(
            result,
            source,
            revision,
            $"{GetCreateElementLabel(kind)} created in {destinationLabel}");
    }

    private void DuplicateSelectedElement()
    {
        if (!TryGetAuthoringContext(
                out SvgDocumentIndex document,
                out SvgElementNode? selection,
                out string source,
                out long revision,
                "Duplicate"))
        {
            return;
        }

        SvgAuthoringEditResult result = _svgElementDuplicateService.CreateEdit(
            source,
            document,
            selection,
            _viewModel.Inspector.IsElementEffectivelyLocked);
        ApplyAuthoringEdit(result, source, revision, "Element duplicated");
    }

    private void DeleteSelectedElement()
    {
        if (!TryGetAuthoringContext(
                out SvgDocumentIndex document,
                out SvgElementNode? selection,
                out string source,
                out long revision,
                "Delete"))
        {
            return;
        }

        SvgAuthoringEditResult result = _svgElementDeleteService.CreateEdit(
            source,
            document,
            selection,
            _viewModel.Inspector.IsElementEffectivelyLocked);
        ApplyAuthoringEdit(result, source, revision, "Element deleted");
    }

    private void MoveSelectedElementToRoot()
    {
        if (!TryGetAuthoringContext(
                out SvgDocumentIndex document,
                out SvgElementNode? selection,
                out string source,
                out long revision,
                "Move to SVG Root")
            || selection is null)
        {
            return;
        }

        SvgAuthoringEditResult result =
            _svgLayerReparentService.CreateMoveToRootFrontEdit(
                source,
                document,
                selection,
                _viewModel.Inspector.IsElementEffectivelyLocked);
        ApplyAuthoringEdit(
            result,
            source,
            revision,
            "Element moved to the front of the SVG root");
    }

    private bool ApplyAuthoringEdit(
        SvgAuthoringEditResult result,
        string sourceSnapshot,
        long expectedRevision,
        string successStatus)
    {
        if (!result.IsSuccess
            || result.Edit is null
            || result.PreferredSelection is null)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage ?? "The visual authoring operation was rejected.");
            return false;
        }
        if (result.RequiresConfirmation)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                result.ConfirmationMessage
                ?? "Delete the selected element and its descendants?",
                "Delete SVG group",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
            {
                _viewModel.SetOperationStatus("Delete cancelled");
                return false;
            }
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(sourceSnapshot, StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; select the element and try again.");
            return false;
        }

        CancelOpacitySliderGesture();
        CancelVisualEditGesture();
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        _previewDebouncer.Cancel();
        string updatedSource = SourceEditor.Text;
        long updatedRevision = _sourceRevisionTracker.Current;
        SvgDocumentIndexResult rebuilt = _documentIndexService.Build(updatedSource);
        ApplyValidationResult(
            updatedSource,
            updatedRevision,
            rebuilt,
            result.PreferredSelection);
        _viewModel.SetOperationStatus(successStatus);
        return true;
    }

    private bool TryGetAuthoringContext(
        out SvgDocumentIndex document,
        out SvgElementNode? selection,
        out string source,
        out long revision,
        string operationName)
    {
        document = null!;
        selection = null;
        source = SourceEditor.Text;
        revision = _inspectorSourceRevision;
        if (_viewModel.Inspector.DocumentIndex is not SvgDocumentIndex current
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                revision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive))
        {
            _viewModel.SetOperationStatus(
                $"{operationName} is unavailable until the current SVG is valid and indexed.");
            return false;
        }

        document = current;
        selection = _viewModel.Inspector.SelectedElement?.Element;
        return true;
    }

    private void RefreshAuthoringControls()
    {
        SvgAuthoringAvailability availability = GetCreateAvailability();
        AddElementButton.IsEnabled = availability.CanExecute;
        string help = availability.CanExecute
            ? "Choose the SVG root or the selected safe layer context before adding an element."
            : availability.UnavailableReason
                ?? "Creation is unavailable for the current source.";
        AddElementButton.ToolTip = help;
        AutomationProperties.SetHelpText(AddElementButton, help);
    }

    private SvgAuthoringAvailability GetCreateAvailability()
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        if (document is null
            || !_inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive))
        {
            return new SvgAuthoringAvailability(
                false,
                "Creation is unavailable until the current SVG is valid and indexed.");
        }

        return _svgElementCreationService.GetAvailability(
            SourceEditor.Text,
            document,
            _viewModel.Inspector.SelectedElement?.Element,
            SvgCreateDestination.SvgRoot,
            _viewModel.Inspector.IsElementEffectivelyLocked);
    }

    private void UpdateAuthoringMenuItems(ItemCollection items)
    {
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        SvgElementNode? selected = _viewModel.Inspector.SelectedElement?.Element;
        string source = SourceEditor.Text;
        SvgAuthoringAvailability unavailable = new(
            false,
            "The current SVG must be valid, indexed, and current.");
        bool isCurrent = document is not null
            && _inspectorSourceGuard.CanUseIndex(
                _isInspectorIndexCurrent,
                _inspectorSourceRevision,
                _sourceRevisionTracker.Current,
                _isEditorTextCompositionActive);
        SvgAuthoringAvailability createRoot = isCurrent
            ? _svgElementCreationService.GetAvailability(
                source,
                document!,
                selected,
                SvgCreateDestination.SvgRoot,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;
        SvgAuthoringAvailability createContext = isCurrent
            ? _svgElementCreationService.GetAvailability(
                source,
                document!,
                selected,
                SvgCreateDestination.SelectedContext,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;
        SvgAuthoringAvailability duplicate = isCurrent
            ? _svgElementDuplicateService.GetAvailability(
                source,
                document!,
                selected,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;
        SvgAuthoringAvailability delete = isCurrent
            ? _svgElementDeleteService.GetAvailability(
                source,
                document!,
                selected,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;
        SvgAuthoringAvailability moveRoot = isCurrent
            ? _svgLayerReparentService.GetMoveToRootAvailability(
                source,
                document!,
                selected,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;
        SvgAuthoringAvailability rename = isCurrent
            ? _svgLayerRenameService.GetAvailability(
                source,
                document!,
                selected,
                _viewModel.Inspector.IsElementEffectivelyLocked)
            : unavailable;

        foreach (MenuItem item in EnumerateMenuItems(items))
        {
            if (item.Tag is string destinationTag
                && destinationTag.StartsWith(
                    "CreateDestination:",
                    StringComparison.Ordinal))
            {
                bool isRoot = destinationTag.EndsWith(
                    ":Root",
                    StringComparison.Ordinal);
                SvgAuthoringAvailability destinationAvailability = isRoot
                    ? createRoot
                    : createContext;
                if (!isRoot)
                {
                    item.Header = GetSelectedCreationContextHeader(
                        document,
                        selected);
                }
                item.IsEnabled = destinationAvailability.CanExecute;
                item.ToolTip = destinationAvailability.UnavailableReason;
                AutomationProperties.SetHelpText(
                    item,
                    destinationAvailability.CanExecute
                        ? item.Header?.ToString() ?? "Creation destination"
                        : destinationAvailability.UnavailableReason
                            ?? "Creation destination unavailable");
                continue;
            }

            SvgAuthoringAvailability? availability = item.Tag switch
            {
                string tag when tag.StartsWith("Create:Root:", StringComparison.Ordinal) => createRoot,
                string tag when tag.StartsWith("Create:Context:", StringComparison.Ordinal) => createContext,
                "Duplicate" => duplicate,
                "Delete" => delete,
                "MoveToRoot" => moveRoot,
                "Rename" => rename,
                _ => null
            };
            if (availability is null)
            {
                continue;
            }
            item.IsEnabled = availability.CanExecute;
            item.ToolTip = availability.UnavailableReason;
            AutomationProperties.SetHelpText(
                item,
                availability.CanExecute
                    ? item.Header?.ToString() ?? "SVG authoring command"
                    : availability.UnavailableReason ?? "Command unavailable");
        }
    }

    private static IEnumerable<MenuItem> EnumerateMenuItems(ItemCollection items)
    {
        foreach (object entry in items)
        {
            if (entry is not MenuItem item)
            {
                continue;
            }
            yield return item;
            foreach (MenuItem child in EnumerateMenuItems(item.Items))
            {
                yield return child;
            }
        }
    }

    private static bool TryReadCreateElementCommand(
        object? value,
        out SvgCreateDestination destination,
        out SvgCreateElementKind kind)
    {
        destination = default;
        kind = default;
        if (value is not string text)
        {
            return false;
        }

        string[] parts = text.Split(':');
        if (parts.Length != 3
            || !parts[0].Equals("Create", StringComparison.Ordinal))
        {
            return false;
        }

        destination = parts[1] switch
        {
            "Root" => SvgCreateDestination.SvgRoot,
            "Context" => SvgCreateDestination.SelectedContext,
            _ => (SvgCreateDestination)(-1)
        };
        return Enum.IsDefined(destination)
            && Enum.TryParse(parts[2], false, out kind)
            && Enum.IsDefined(kind);
    }

    private static string GetSelectedCreationContextHeader(
        SvgDocumentIndex? document,
        SvgElementNode? selection)
    {
        if (document is null || selection is null)
        {
            return "In Selected Conte_xt";
        }
        if (SvgLayerPolicy.IsGroup(selection.Name))
        {
            return $"Inside {selection.DisplayLabel}";
        }

        SvgElementNode? parent = document.FindParent(selection);
        return parent?.Name == "svg"
            ? $"Alongside {selection.DisplayLabel} at SVG Root"
            : parent is null
                ? "In Selected Conte_xt"
                : $"Alongside {selection.DisplayLabel} in {parent.DisplayLabel}";
    }

    private static string GetCreateElementLabel(SvgCreateElementKind kind) =>
        kind == SvgCreateElementKind.Group
            ? "Group"
            : kind.ToString();

    private void BeginSelectedLayerRename()
    {
        SvgLayerViewModel? layer = _viewModel.Inspector.SelectedLayer;
        if (layer is null)
        {
            _viewModel.SetOperationStatus(
                "Select a current layer in Layers before naming it.");
            return;
        }
        SvgDocumentIndex? document = _viewModel.Inspector.DocumentIndex;
        SvgAuthoringAvailability availability = document is null
            ? new SvgAuthoringAvailability(false, "The current SVG is not indexed.")
            : _svgLayerRenameService.GetAvailability(
                SourceEditor.Text,
                document,
                layer.Element,
                _viewModel.Inspector.IsElementEffectivelyLocked);
        if (!availability.CanExecute)
        {
            _viewModel.SetOperationStatus(
                availability.UnavailableReason ?? "The layer cannot be renamed.");
            return;
        }

        layer.BeginRename();
    }

    private void OnLayerRenameTextBoxIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsVisible)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (textBox.IsVisible)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }));
        }
    }

    private void OnLayerRenameTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: SvgLayerViewModel layer })
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (TryHandleLayerRenameKey((TextBox)sender, layer, key))
        {
            e.Handled = true;
        }
    }

    private bool TryHandleLayerRenameKey(
        TextBox textBox,
        SvgLayerViewModel layer,
        Key key)
    {
        if (!textBox.IsKeyboardFocusWithin || !layer.IsRenaming)
        {
            return false;
        }

        if (key == Key.Escape)
        {
            layer.EndRename();
            QueueFocusSelectedLayerRow();
            _viewModel.SetOperationStatus("Rename cancelled");
            return true;
        }
        if (key != Key.Enter || _isInspectorTextCompositionActive)
        {
            return false;
        }

        CommitLayerRename(
            layer,
            cancelOnFailure: false,
            restoreLayerFocus: true);
        return true;
    }

    private void OnLayerRenameTextBoxLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { Tag: SvgLayerViewModel { IsRenaming: true } layer }
            && !_isInspectorTextCompositionActive)
        {
            CommitLayerRename(
                layer,
                cancelOnFailure: true,
                restoreLayerFocus: false);
        }
    }

    private void CommitLayerRename(
        SvgLayerViewModel layer,
        bool cancelOnFailure,
        bool restoreLayerFocus)
    {
        if (!layer.IsRenaming
            || !TryGetAuthoringContext(
                out SvgDocumentIndex document,
                out _,
                out string source,
                out long revision,
                "Name layer"))
        {
            if (cancelOnFailure)
            {
                layer.EndRename();
            }
            return;
        }

        SvgAuthoringEditResult result = _svgLayerRenameService.CreateEdit(
            source,
            document,
            layer.Element,
            layer.RenameText,
            _viewModel.Inspector.IsElementEffectivelyLocked);
        if (!result.IsSuccess)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage ?? "The layer could not be renamed.");
            if (cancelOnFailure)
            {
                // A rejected focus-loss commit must not leave an invisible or
                // stale editing transaction that can interfere with Save.
                layer.EndRename();
            }
            return;
        }

        string friendlyName = layer.RenameText;
        // End edit mode before applying the source change. Collapsing the
        // TextBox raises LostKeyboardFocus synchronously/asynchronously, but
        // that path now observes IsRenaming == false and cannot commit twice.
        layer.EndRename();
        bool applied = ApplyAuthoringEdit(
            result,
            source,
            revision,
            friendlyName.Length == 0
                ? "Friendly layer name removed"
                : $"Layer named {friendlyName}");
        if (restoreLayerFocus || !applied)
        {
            QueueFocusSelectedLayerRow();
        }
    }

    private void QueueFocusSelectedLayerRow()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                SvgLayerViewModel? selected =
                    _viewModel.Inspector.SelectedLayer;
                TreeViewItem? item = selected is null
                    ? null
                    : FindLayerContainer(LayersTree, selected);
                if (item is not null)
                {
                    item.BringIntoView();
                    item.Focus();
                    return;
                }

                LayersTree.Focus();
            }));
    }

    private static TreeViewItem? FindLayerContainer(
        ItemsControl parent,
        SvgLayerViewModel layer)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(layer)
            is TreeViewItem direct)
        {
            return direct;
        }

        foreach (object child in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(child)
                is not TreeViewItem childContainer)
            {
                continue;
            }

            TreeViewItem? nested = FindLayerContainer(childContainer, layer);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private bool TryHandleAuthoringShortcut(
        ModifierKeys modifiers,
        Key pressedKey)
    {
        if (PreviewWebView.IsKeyboardFocusWithin
            && (pressedKey is Key.Delete or Key.Back
                || (pressedKey == Key.D
                    && modifiers == ModifierKeys.Control)))
        {
            // WebView2 owns these physical key events. The exact-hash trusted
            // page forwards one token/revision-bound command, avoiding duplicate
            // execution between WPF tunneling and the DOM.
            return false;
        }

        bool authoringFocus = LayersTree.IsKeyboardFocusWithin
            || InspectorTree.IsKeyboardFocusWithin
            || InspectorPropertiesPanel.IsKeyboardFocusWithin
            || PreviewWebView.IsKeyboardFocusWithin;
        SvgAuthoringShortcutAction action = SvgAuthoringShortcutRouter.Resolve(
            modifiers,
            pressedKey,
            authoringFocus,
            IsEditableControlFocused(),
            _isEditorTextCompositionActive
                || _isInspectorTextCompositionActive);
        if (action == SvgAuthoringShortcutAction.Duplicate)
        {
            DuplicateSelectedElement();
            return true;
        }
        if (action == SvgAuthoringShortcutAction.Delete)
        {
            if (_viewModel.Inspector.SelectedElement is not null)
            {
                DeleteSelectedElement();
            }
            return true;
        }

        return false;
    }

    private void OnInspectorTreePreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject originalSource)
        {
            return;
        }

        TreeViewItem? item = FindVisualAncestor<TreeViewItem>(originalSource);
        if (item is null)
        {
            return;
        }
        item.IsSelected = true;
        item.Focus();
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
        if (sender is not TreeView { ContextMenu: ContextMenu contextMenu })
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
        UpdateAuthoringMenuItems(contextMenu.Items);
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
        if (_viewModel.Inspector.IsElementEffectivelyLocked(element))
        {
            return new SvgLayerOrderAvailability(
                false,
                "Unlock the layer and its parent group before arranging it.");
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
        if (_viewModel.Inspector.IsElementEffectivelyLocked(opacity.Element))
        {
            opacity.ErrorMessage =
                "Unlock this layer or its parent group to edit opacity.";
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
                    LayersTree.IsKeyboardFocusWithin
                        || InspectorTree.IsKeyboardFocusWithin,
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
        if (_viewModel.Inspector.IsElementEffectivelyLocked(property.Element))
        {
            property.ErrorMessage =
                "Unlock this layer or its parent group to edit properties.";
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
