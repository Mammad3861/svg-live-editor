using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private readonly SvgVisualGeometryIndexService
        _visualGeometryIndexService = new();
    private readonly PreviewSvgCoordinateMapper
        _previewSvgCoordinateMapper = new();
    private readonly SvgVisualHitTestService _visualHitTestService = new();
    private readonly SvgVisualMoveService _visualMoveService = new();
    private readonly SvgVisualResizeService _visualResizeService = new();
    private readonly SvgVisualResizeHandleService
        _visualResizeHandleService = new();
    private readonly PreviewVisualInteractionMessageParser
        _previewVisualInteractionMessageParser = new();
    private readonly VisualEditingReadinessPolicy
        _visualEditingReadinessPolicy = new();
    private readonly PreviewTextMeasurementMessageParser
        _previewTextMeasurementMessageParser = new();
    private readonly SvgVisualTextMeasurementService
        _visualTextMeasurementService = new();

    private SvgVisualDocument? _lastValidVisualDocument;
    private SvgVisualDocument? _activePreviewVisualDocument;
    private SvgVisualDocument? _visiblePreviewVisualDocument;
    private long? _lastValidVisualSourceRevision;
    private long? _activePreviewSourceRevision;
    private long? _visiblePreviewSourceRevision;
    private SvgElementIdentity? _visualSelectionIdentity;
    private string? _visualSelectionBridgeId;
    private VisualEditGesture? _visualEditGesture;
    private VisualResizeGesture? _visualResizeGesture;
    private readonly HashSet<string> _consumedVisualResizeGestureIds =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _visualResizeGestureIdOrder = new();
    private PendingPreviewTextMeasurement?
        _pendingPreviewTextMeasurement;

    private bool TryHandlePreviewVisualInteraction(
        string messageJson,
        string bridgeToken)
    {
        if (_visiblePreviewSourceRevision is not long sourceRevision)
        {
            return false;
        }

        if (_previewVisualInteractionMessageParser.TryParsePointer(
                messageJson,
                bridgeToken,
                sourceRevision,
                out PreviewVisualPointerMessage pointer))
        {
            HandlePreviewVisualPointer(pointer);
            return true;
        }

        if (_visualSelectionBridgeId is string selectionId
            && _previewVisualInteractionMessageParser.TryParseResizePointer(
                messageJson,
                bridgeToken,
                sourceRevision,
                selectionId,
                out PreviewVisualResizePointerMessage resizePointer))
        {
            HandlePreviewVisualResizePointer(resizePointer);
            return true;
        }

        if (_previewVisualInteractionMessageParser.TryParseNudge(
                messageJson,
                bridgeToken,
                sourceRevision,
                out PreviewVisualNudgeRequest nudge))
        {
            HandlePreviewVisualNudge(nudge);
            return true;
        }

        return false;
    }

    private bool TryHandlePreviewTextMeasurements(
        string messageJson,
        string bridgeToken)
    {
        if (_pendingPreviewTextMeasurement
                is not PendingPreviewTextMeasurement pending
            || !pending.Token.Equals(
                bridgeToken,
                StringComparison.Ordinal)
            || !_previewTextMeasurementMessageParser.TryParse(
                messageJson,
                pending,
                out IReadOnlyList<SvgVisualTextMeasurementResult> results)
            || _visiblePreviewSourceRevision != pending.SourceRevision
            || _visiblePreviewVisualDocument is not SvgVisualDocument visible)
        {
            return false;
        }

        _pendingPreviewTextMeasurement = null;
        _visiblePreviewVisualDocument =
            _visualTextMeasurementService.Apply(visible, results);
        if (_lastValidVisualSourceRevision == pending.SourceRevision
            && _lastValidVisualDocument is SvgVisualDocument lastValid)
        {
            _lastValidVisualDocument =
                _visualTextMeasurementService.Apply(lastValid, results);
        }

        ShowVisualSelection();
        return true;
    }

    private void HandlePreviewVisualPointer(
        PreviewVisualPointerMessage pointer)
    {
        if (pointer.Phase == PreviewVisualPointerPhase.Cancel)
        {
            CancelVisualEditGesture();
            return;
        }

        if (pointer.Phase == PreviewVisualPointerPhase.Down)
        {
            BeginVisualEditGesture(pointer);
            return;
        }

        if (_visualEditGesture is not VisualEditGesture gesture
            || !string.Equals(
                gesture.GestureId,
                pointer.GestureId,
                StringComparison.Ordinal)
            || gesture.SourceRevision != pointer.SourceRevision)
        {
            return;
        }

        if (pointer.Phase == PreviewVisualPointerPhase.Move)
        {
            UpdateVisualEditGesture(pointer, gesture);
        }
        else if (pointer.Phase == PreviewVisualPointerPhase.Up)
        {
            CompleteVisualEditGesture(pointer, gesture);
        }
    }

    private void HandlePreviewVisualResizePointer(
        PreviewVisualResizePointerMessage pointer)
    {
        if (pointer.Phase == PreviewVisualPointerPhase.Cancel)
        {
            if (_visualResizeGesture is VisualResizeGesture active
                && active.GestureId.Equals(
                    pointer.GestureId,
                    StringComparison.Ordinal))
            {
                CancelVisualEditGesture();
            }
            return;
        }

        if (pointer.Phase == PreviewVisualPointerPhase.Down)
        {
            BeginVisualResizeGesture(pointer);
            return;
        }

        if (_visualResizeGesture is not VisualResizeGesture gesture
            || !gesture.GestureId.Equals(
                pointer.GestureId,
                StringComparison.Ordinal)
            || !gesture.SelectionId.Equals(
                pointer.SelectionId,
                StringComparison.Ordinal)
            || gesture.SourceRevision != pointer.SourceRevision
            || gesture.Handle != pointer.Handle)
        {
            return;
        }

        if (pointer.Phase == PreviewVisualPointerPhase.Move)
        {
            UpdateVisualResizeGesture(pointer, gesture);
        }
        else if (pointer.Phase == PreviewVisualPointerPhase.Up)
        {
            CompleteVisualResizeGesture(pointer, gesture);
        }
    }

    private void BeginVisualResizeGesture(
        PreviewVisualResizePointerMessage pointer)
    {
        CancelVisualEditGesture();
        if (!CanUseVisualEditing(pointer.SourceRevision)
            || pointer.Button != 0
            || (pointer.Buttons & 1) == 0
            || Mouse.LeftButton != MouseButtonState.Pressed
            || HasResizeBlockingModifier(pointer)
            || _consumedVisualResizeGestureIds.Contains(pointer.GestureId)
            || _visualSelectionIdentity
                is not SvgElementIdentity identity
            || _visualSelectionBridgeId is not string selectionId
            || !selectionId.Equals(
                pointer.SelectionId,
                StringComparison.Ordinal)
            || _visiblePreviewVisualDocument
                is not SvgVisualDocument visualDocument
            || visualDocument.FindElement(identity)
                is not SvgVisualElement element
            || element.Geometry is not SvgVisualShapeGeometry geometry
            || !_visualResizeHandleService.IsAllowed(
                element,
                pointer.Handle)
            || !_previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped)
            || !IsResizeHandleHit(
                element,
                geometry,
                pointer.Handle,
                mapped))
        {
            return;
        }

        RememberVisualResizeGestureId(pointer.GestureId);
        _visualResizeGesture = new VisualResizeGesture(
            pointer.GestureId,
            pointer.SelectionId,
            pointer.SourceRevision,
            identity,
            pointer.Handle,
            geometry);
        _viewModel.SetOperationStatus(
            $"{element.SourceElement.Name} resize started");
    }

    private void UpdateVisualResizeGesture(
        PreviewVisualResizePointerMessage pointer,
        VisualResizeGesture gesture)
    {
        string? resizeError = null;
        if (!CanContinueVisualResizeGesture(
                pointer,
                gesture,
                requirePressedButton: true)
            || _visiblePreviewVisualDocument
                is not SvgVisualDocument visualDocument
            || visualDocument.FindElement(gesture.ElementIdentity)
                is not SvgVisualElement element
            || !_previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped)
            || !_visualResizeService.TryCalculate(
                element,
                gesture.Handle,
                mapped.Point,
                pointer.ShiftHeld,
                out SvgVisualShapeGeometry resized,
                out resizeError))
        {
            CancelVisualEditGesture(
                resizeError ?? "Visual resize cancelled");
            return;
        }

        _visualResizeGesture = gesture with
        {
            PreviewGeometry = resized
        };
        ShowVisualSelection(geometryOverride: resized);
        _viewModel.SetOperationStatus(
            FormatResizeStatus(element.Kind, resized));
    }

    private void CompleteVisualResizeGesture(
        PreviewVisualResizePointerMessage pointer,
        VisualResizeGesture gesture)
    {
        if (!CanContinueVisualResizeGesture(
                pointer,
                gesture,
                requirePressedButton: false))
        {
            CancelVisualEditGesture();
            return;
        }

        VisualResizeGesture completed = _visualResizeGesture ?? gesture;
        string? resizeError = null;
        if (_visiblePreviewVisualDocument
                is not SvgVisualDocument visualDocument
            || visualDocument.FindElement(completed.ElementIdentity)
                is not SvgVisualElement element
            || !_previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped)
            || !_visualResizeService.TryCalculate(
                element,
                completed.Handle,
                mapped.Point,
                pointer.ShiftHeld,
                out SvgVisualShapeGeometry resized,
                out resizeError))
        {
            CancelVisualEditGesture(
                resizeError ?? "Visual resize cancelled");
            return;
        }

        _visualResizeGesture = null;
        ApplyVisualResize(
            completed.ElementIdentity,
            resized);
    }

    private void ApplyVisualResize(
        SvgElementIdentity identity,
        SvgVisualShapeGeometry resizedGeometry)
    {
        if (!CanUseVisualEditing(_sourceRevisionTracker.Current)
            || _lastValidVisualDocument?.FindElement(identity)
                is not SvgVisualElement element)
        {
            _viewModel.SetOperationStatus(
                "Visual editing paused until the current SVG is valid.");
            ShowVisualSelection();
            return;
        }

        long expectedRevision = _sourceRevisionTracker.Current;
        string sourceSnapshot = SourceEditor.Text;
        SvgAttributeEditResult result = _visualResizeService.CreateEdit(
            sourceSnapshot,
            element,
            resizedGeometry);
        if (!result.IsSuccess)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage
                ?? "The selected element could not be resized.");
            ShowVisualSelection();
            return;
        }
        if (result.Edit is null)
        {
            ShowVisualSelection();
            return;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(
                sourceSnapshot,
                StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; select the element again.");
            ShowVisualSelection();
            return;
        }

        _visualSelectionIdentity = identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        _previewDebouncer.Cancel();
        long updatedRevision = _sourceRevisionTracker.Current;
        string updatedSource = SourceEditor.Text;
        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(updatedSource);
        ApplyValidationResult(
            updatedSource,
            updatedRevision,
            rebuilt);
        _viewModel.SetOperationStatus(
            $"{element.SourceElement.Name} resized");
    }

    private void BeginVisualEditGesture(
        PreviewVisualPointerMessage pointer)
    {
        CancelVisualEditGesture();
        if (!CanUseVisualEditing(pointer.SourceRevision)
            || pointer.Button != 0
            || (pointer.Buttons & 1) == 0
            || HasVisualGestureModifier(pointer)
            || _visiblePreviewVisualDocument
                is not SvgVisualDocument visualDocument)
        {
            return;
        }

        if (!_previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped))
        {
            ClearVisualSelection();
            _viewModel.SetOperationStatus("Visual selection cleared");
            return;
        }

        SvgVisualHitTestResult hit =
            _visualHitTestService.HitTestDetailed(visualDocument, mapped);
        if (hit.Blocker is SvgVisualElement blocker)
        {
            ClearVisualSelection();
            _viewModel.SetOperationStatus(
                blocker.UnsupportedReason
                ?? $"Visual editing is not available for {blocker.SourceElement.Name} elements in this version.");
            return;
        }
        if (hit.Element is not SvgVisualElement element)
        {
            ClearVisualSelection();
            _viewModel.SetOperationStatus("Visual selection cleared");
            return;
        }

        SelectVisualElement(element);
        if (!element.IsMovable)
        {
            _viewModel.SetOperationStatus(
                element.UnsupportedReason
                ?? $"{element.SourceElement.Name} is read-only in Select mode.");
            return;
        }

        _visualEditGesture = new VisualEditGesture(
            pointer.GestureId,
            pointer.SourceRevision,
            element.SourceElement.Identity,
            pointer.ViewportPoint,
            mapped.Point,
            0,
            0,
            HasMoved: false);
    }

    private void UpdateVisualEditGesture(
        PreviewVisualPointerMessage pointer,
        VisualEditGesture gesture)
    {
        if (!CanContinueVisualGesture(pointer, gesture)
            || _visiblePreviewVisualDocument
                is not SvgVisualDocument visualDocument
            || !_previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped))
        {
            CancelVisualEditGesture();
            return;
        }

        double clientDeltaX =
            pointer.ViewportPoint.X - gesture.StartViewportPoint.X;
        double clientDeltaY =
            pointer.ViewportPoint.Y - gesture.StartViewportPoint.Y;
        bool hasMoved = gesture.HasMoved
            || Math.Abs(clientDeltaX)
                >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(clientDeltaY)
                >= SystemParameters.MinimumVerticalDragDistance;
        if (!hasMoved)
        {
            return;
        }

        double deltaX = mapped.Point.X - gesture.StartSvgPoint.X;
        double deltaY = mapped.Point.Y - gesture.StartSvgPoint.Y;
        if (!IsSupportedVisualDelta(deltaX, deltaY))
        {
            CancelVisualEditGesture(
                "The requested movement is outside the supported range.");
            return;
        }

        _visualEditGesture = gesture with
        {
            DeltaX = deltaX,
            DeltaY = deltaY,
            HasMoved = true
        };
        ShowVisualSelection(deltaX, deltaY);
        _viewModel.SetOperationStatus(
            $"{GetSelectedElementName()} · Δ {FormatDelta(deltaX)}, {FormatDelta(deltaY)}");
    }

    private void CompleteVisualEditGesture(
        PreviewVisualPointerMessage pointer,
        VisualEditGesture gesture)
    {
        if (!CanContinueVisualGesture(
                pointer,
                gesture,
                requirePressedButton: false))
        {
            CancelVisualEditGesture();
            return;
        }

        VisualEditGesture completed =
            _visualEditGesture ?? gesture;
        if (_visiblePreviewVisualDocument
                is SvgVisualDocument visualDocument
            && _previewSvgCoordinateMapper.TryMap(
                visualDocument.Viewport,
                pointer.Image,
                pointer.ViewportPoint,
                out SvgMappedPreviewPoint mapped))
        {
            double clientDeltaX =
                pointer.ViewportPoint.X
                - completed.StartViewportPoint.X;
            double clientDeltaY =
                pointer.ViewportPoint.Y
                - completed.StartViewportPoint.Y;
            bool hasMoved = completed.HasMoved
                || Math.Abs(clientDeltaX)
                    >= SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(clientDeltaY)
                    >= SystemParameters.MinimumVerticalDragDistance;
            if (hasMoved)
            {
                completed = completed with
                {
                    DeltaX =
                        mapped.Point.X - completed.StartSvgPoint.X,
                    DeltaY =
                        mapped.Point.Y - completed.StartSvgPoint.Y,
                    HasMoved = true
                };
            }
        }
        _visualEditGesture = null;
        if (!completed.HasMoved)
        {
            ShowVisualSelection();
            return;
        }

        ApplyVisualMovement(
            completed.ElementIdentity,
            completed.DeltaX,
            completed.DeltaY,
            isNudge: false);
    }

    private void HandlePreviewVisualNudge(
        PreviewVisualNudgeRequest request)
    {
        if (!PreviewVisualNudgeFocusPolicy.CanRoute(
                PreviewWebView.IsKeyboardFocusWithin,
                SourceEditor.IsKeyboardFocusWithin,
                Keyboard.FocusedElement is TextBoxBase)
            || !CanUseVisualEditing(request.SourceRevision)
            || _visualSelectionIdentity
                is not SvgElementIdentity identity)
        {
            return;
        }

        CancelVisualEditGesture();
        ApplyVisualMovement(
            identity,
            request.DeltaX,
            request.DeltaY,
            isNudge: true);
    }

    private void ApplyVisualMovement(
        SvgElementIdentity identity,
        double deltaX,
        double deltaY,
        bool isNudge)
    {
        if (!CanUseVisualEditing(
                _sourceRevisionTracker.Current)
            || _lastValidVisualDocument?.FindElement(identity)
                is not SvgVisualElement element)
        {
            _viewModel.SetOperationStatus(
                "Visual editing paused until the current SVG is valid.");
            ShowVisualSelection();
            return;
        }

        long expectedRevision = _sourceRevisionTracker.Current;
        string sourceSnapshot = SourceEditor.Text;
        SvgAttributeEditResult result = _visualMoveService.CreateEdit(
            sourceSnapshot,
            element,
            deltaX,
            deltaY);
        if (!result.IsSuccess)
        {
            _viewModel.SetOperationStatus(
                result.ErrorMessage
                ?? "The selected element could not be moved.");
            ShowVisualSelection();
            return;
        }
        if (result.Edit is null)
        {
            ShowVisualSelection();
            return;
        }
        if (!_sourceRevisionTracker.IsCurrent(expectedRevision)
            || !SourceEditor.Text.Equals(
                sourceSnapshot,
                StringComparison.Ordinal))
        {
            _viewModel.SetOperationStatus(
                "The source changed; select the element again.");
            ShowVisualSelection();
            return;
        }

        _visualSelectionIdentity = identity;
        _documentEditService.Apply(SourceEditor.Document, result.Edit);
        _previewDebouncer.Cancel();
        long updatedRevision = _sourceRevisionTracker.Current;
        string updatedSource = SourceEditor.Text;
        SvgDocumentIndexResult rebuilt =
            _documentIndexService.Build(updatedSource);
        ApplyValidationResult(
            updatedSource,
            updatedRevision,
            rebuilt);
        _viewModel.SetOperationStatus(
            isNudge
                ? $"{element.SourceElement.Name} nudged by {FormatDelta(deltaX)}, {FormatDelta(deltaY)}"
                : $"{element.SourceElement.Name} moved by {FormatDelta(deltaX)}, {FormatDelta(deltaY)}");
    }

    private bool CanUseVisualEditing(long sourceRevision)
    {
        return sourceRevision == _sourceRevisionTracker.Current
            && _visualEditingReadinessPolicy.IsReady(
                new VisualEditingReadiness(
                    _isPanModeEnabled,
                    _previewPngSourceState
                        == PreviewPngSourceState.CurrentValid,
                    _isInspectorIndexCurrent,
                    _inspectorSourceRevision,
                    _sourceRevisionTracker.Current,
                    _lastValidVisualSourceRevision,
                    _visiblePreviewSourceRevision,
                    _isPreviewNavigationRequested
                    || _activePreviewNavigationId is not null
                    || _activePreviewRevision is not null));
    }

    private bool CanContinueVisualGesture(
        PreviewVisualPointerMessage pointer,
        VisualEditGesture gesture,
        bool requirePressedButton = true)
    {
        return CanUseVisualEditing(pointer.SourceRevision)
            && string.Equals(
                pointer.GestureId,
                gesture.GestureId,
                StringComparison.Ordinal)
            && pointer.Button == 0
            && (!requirePressedButton || (pointer.Buttons & 1) != 0)
            && !HasVisualGestureModifier(pointer);
    }

    private bool CanContinueVisualResizeGesture(
        PreviewVisualResizePointerMessage pointer,
        VisualResizeGesture gesture,
        bool requirePressedButton)
    {
        bool physicalButtonMatches = requirePressedButton
            ? (pointer.Buttons & 1) != 0
                && Mouse.LeftButton == MouseButtonState.Pressed
            : (pointer.Buttons & 1) == 0
                && Mouse.LeftButton == MouseButtonState.Released;
        return CanUseVisualEditing(pointer.SourceRevision)
            && pointer.Button == 0
            && physicalButtonMatches
            && !HasResizeBlockingModifier(pointer)
            && pointer.GestureId.Equals(
                gesture.GestureId,
                StringComparison.Ordinal)
            && pointer.SelectionId.Equals(
                gesture.SelectionId,
                StringComparison.Ordinal)
            && pointer.SourceRevision == gesture.SourceRevision
            && pointer.Handle == gesture.Handle
            && _visualSelectionIdentity == gesture.ElementIdentity
            && _visualSelectionBridgeId?.Equals(
                gesture.SelectionId,
                StringComparison.Ordinal) == true;
    }

    private static bool HasVisualGestureModifier(
        PreviewVisualPointerMessage pointer)
    {
        return pointer.ControlHeld
            || pointer.ShiftHeld
            || pointer.AltHeld
            || pointer.MetaHeld
            || pointer.SpaceHeld;
    }

    private static bool HasResizeBlockingModifier(
        PreviewVisualResizePointerMessage pointer)
    {
        return pointer.ControlHeld
            || pointer.AltHeld
            || pointer.MetaHeld
            || pointer.SpaceHeld;
    }

    private bool IsResizeHandleHit(
        SvgVisualElement element,
        SvgVisualShapeGeometry geometry,
        SvgResizeHandle handle,
        SvgMappedPreviewPoint mapped)
    {
        SvgResizeHandleDefinition definition =
            _visualResizeHandleService.Create(element, geometry)
                .Single(item => item.Handle == handle);
        return Math.Abs(mapped.Point.X - definition.Point.X)
                <= mapped.HitTolerance
            && Math.Abs(mapped.Point.Y - definition.Point.Y)
                <= mapped.HitTolerance;
    }

    private void RememberVisualResizeGestureId(string gestureId)
    {
        const int maximumRememberedGestures = 64;
        if (!_consumedVisualResizeGestureIds.Add(gestureId))
        {
            return;
        }

        _visualResizeGestureIdOrder.Enqueue(gestureId);
        while (_visualResizeGestureIdOrder.Count > maximumRememberedGestures)
        {
            _consumedVisualResizeGestureIds.Remove(
                _visualResizeGestureIdOrder.Dequeue());
        }
    }

    private static string FormatResizeStatus(
        SvgVisualElementKind kind,
        SvgVisualShapeGeometry geometry)
    {
        SvgVisualBounds bounds = geometry.Bounds;
        return kind switch
        {
            SvgVisualElementKind.Circle =>
                $"Circle radius {FormatDelta(bounds.Width / 2)}",
            SvgVisualElementKind.Ellipse =>
                $"Ellipse radii {FormatDelta(bounds.Width / 2)} × {FormatDelta(bounds.Height / 2)}",
            SvgVisualElementKind.Line =>
                $"Line {FormatDelta(geometry.X1)}, {FormatDelta(geometry.Y1)} to {FormatDelta(geometry.X2)}, {FormatDelta(geometry.Y2)}",
            _ =>
                $"{FormatDelta(bounds.Width)} × {FormatDelta(bounds.Height)}"
        };
    }

    private void SelectVisualElement(SvgVisualElement element)
    {
        CancelOpacitySliderGesture();
        SetVisualSelectionIdentity(element.SourceElement.Identity);
        SvgElementViewModel? previous =
            _viewModel.Inspector.SelectedElement;
        _viewModel.Inspector.SelectNode(
            element.SourceElement,
            InspectorSelectionOrigin.PreviewNavigation);
        if (ReferenceEquals(
                previous,
                _viewModel.Inspector.SelectedElement)
            && _viewModel.Inspector.SelectedElement
                is SvgElementViewModel selected)
        {
            NavigateToInspectorElement(
                selected,
                InspectorSelectionOrigin.PreviewNavigation);
        }

        ShowVisualSelection();
        _viewModel.SetOperationStatus(
            $"{element.SourceElement.Name} selected");
        RefreshSelectedTextWarnings();
    }

    private void ClearVisualSelection()
    {
        _visualSelectionIdentity = null;
        _visualSelectionBridgeId = null;
        _visualEditGesture = null;
        _visualResizeGesture = null;
        _viewModel.Inspector.SelectNode(
            null,
            InspectorSelectionOrigin.PreviewNavigation);
        PostVisualSelection(selection: null);
        RefreshSelectedTextWarnings();
    }

    private void SynchronizeVisualSelectionFromInspector(
        bool announce = false)
    {
        SvgElementIdentity? identity =
            _viewModel.Inspector.SelectedElement?.Element.Identity;
        SetVisualSelectionIdentity(identity);
        if (identity is null)
        {
            PostVisualSelection(selection: null);
            return;
        }

        SvgVisualElement? visualElement =
            _lastValidVisualDocument?.FindElement(identity);
        if (visualElement is null || !visualElement.IsSelectable)
        {
            PostVisualSelection(selection: null);
            if (announce)
            {
                string elementName =
                    _viewModel.Inspector.SelectedElement?.Element.Name
                    ?? "This element";
                _viewModel.SetOperationStatus(
                    visualElement?.UnsupportedReason
                    ?? $"{elementName} is not supported by the Select tool.");
            }
            return;
        }

        ShowVisualSelection();
        if (announce && !visualElement.IsMovable)
        {
            _viewModel.SetOperationStatus(
                visualElement.UnsupportedReason
                ?? $"{visualElement.SourceElement.Name} is read-only in Select mode.");
        }
    }

    private void ShowVisualSelection(
        double deltaX = 0,
        double deltaY = 0,
        SvgVisualShapeGeometry? geometryOverride = null)
    {
        if (_visualSelectionIdentity
                is not SvgElementIdentity identity
            || _visiblePreviewVisualDocument?.FindElement(identity)
                is not SvgVisualElement
                {
                    Geometry: SvgVisualShapeGeometry sourceGeometry
                } element)
        {
            PostVisualSelection(selection: null);
            return;
        }

        string selectionId = EnsureVisualSelectionBridgeId();
        SvgVisualShapeGeometry geometry =
            geometryOverride ?? sourceGeometry;
        PostVisualSelection(new PreviewVisualSelection(
            element.Kind,
            geometry,
            deltaX,
            deltaY,
            selectionId,
            _visualResizeHandleService.Create(element, geometry)));
    }

    private void SetVisualSelectionIdentity(SvgElementIdentity? identity)
    {
        if (_visualSelectionIdentity == identity)
        {
            return;
        }

        _visualSelectionIdentity = identity;
        _visualSelectionBridgeId = identity is null
            ? null
            : CreateVisualSelectionBridgeId();
    }

    private string EnsureVisualSelectionBridgeId()
    {
        return _visualSelectionBridgeId ??=
            CreateVisualSelectionBridgeId();
    }

    private static string CreateVisualSelectionBridgeId() =>
        Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    private void PostVisualSelection(
        PreviewVisualSelection? selection)
    {
        if (!_isWebViewReady
            || _activePreviewBridgeToken is not string token
            || _visiblePreviewSourceRevision
                is not long sourceRevision
            || PreviewWebView.CoreWebView2
                is not CoreWebView2 core)
        {
            return;
        }

        try
        {
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildVisualSelectionMessage(
                    token,
                    sourceRevision,
                    selection));
        }
        catch (InvalidOperationException)
        {
            // Navigation/disposal races are handled by the next ready state.
        }
    }

    private void CancelVisualEditGesture(string? status = null)
    {
        if (_visualEditGesture is null && _visualResizeGesture is null)
        {
            return;
        }

        _visualEditGesture = null;
        _visualResizeGesture = null;
        ShowVisualSelection();
        if (!string.IsNullOrWhiteSpace(status))
        {
            _viewModel.SetOperationStatus(status);
        }
    }

    private void OnVisualSourceChanged()
    {
        CancelVisualEditGesture();
        _visualSelectionBridgeId = null;
        PostVisualSelection(selection: null);
    }

    private void OnVisualDocumentLoaded()
    {
        _visualEditGesture = null;
        _visualResizeGesture = null;
        _visualSelectionIdentity = null;
        _visualSelectionBridgeId = null;
        _consumedVisualResizeGestureIds.Clear();
        _visualResizeGestureIdOrder.Clear();
        PostVisualSelection(selection: null);
    }

    private void InitializeLastValidVisualDocument(
        string source,
        SvgCanvasSize canvasSize,
        long sourceRevision)
    {
        SvgDocumentIndexResult indexResult =
            _documentIndexService.Build(source);
        if (indexResult.Document is not SvgDocumentIndex document)
        {
            return;
        }

        _lastValidVisualDocument =
            _visualGeometryIndexService.Build(document, canvasSize, source);
        _lastValidVisualSourceRevision = sourceRevision;
    }

    private void OnVisualValidationCompleted(
        SvgDocumentIndexResult indexResult,
        SvgCanvasSize canvasSize,
        long sourceRevision,
        string source)
    {
        if (indexResult.Document is not SvgDocumentIndex document)
        {
            _visualSelectionIdentity = null;
            _visualSelectionBridgeId = null;
            PostVisualSelection(selection: null);
            return;
        }

        _lastValidVisualDocument =
            _visualGeometryIndexService.Build(document, canvasSize, source);
        _lastValidVisualSourceRevision = sourceRevision;
    }

    private void OnVisualInspectorResultApplied()
    {
        SynchronizeVisualSelectionFromInspector();
        RefreshSelectedTextWarnings();
    }

    private void OnVisualPreviewNavigationStarted(
        SvgVisualDocument visualDocument,
        long sourceRevision)
    {
        CancelVisualEditGesture();
        CancelOpacitySliderGesture();
        _pendingPreviewTextMeasurement = null;
        _activePreviewVisualDocument = visualDocument;
        _activePreviewSourceRevision = sourceRevision;
    }

    private void OnVisualPreviewNavigationCompleted(bool isSuccess)
    {
        if (isSuccess)
        {
            _visiblePreviewVisualDocument =
                _activePreviewVisualDocument;
            _visiblePreviewSourceRevision =
                _activePreviewSourceRevision;
        }

        _activePreviewVisualDocument = null;
        _activePreviewSourceRevision = null;
        if (isSuccess)
        {
            ShowVisualSelection();
            RequestPreviewTextMeasurements();
        }
    }

    private void RequestPreviewTextMeasurements()
    {
        if (!_isWebViewReady
            || _activePreviewBridgeToken is not string token
            || _visiblePreviewSourceRevision is not long sourceRevision
            || _visiblePreviewVisualDocument is not SvgVisualDocument document
            || PreviewWebView.CoreWebView2 is not CoreWebView2 core)
        {
            return;
        }

        SvgVisualTextMeasurementSpec[] items = document.Elements
            .Select(element => element.TextMeasurement)
            .OfType<SvgVisualTextMeasurementSpec>()
            .ToArray();
        if (items.Length == 0)
        {
            _pendingPreviewTextMeasurement = null;
            return;
        }

        string requestId =
            Convert.ToHexString(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        PendingPreviewTextMeasurement pending = new(
            token,
            sourceRevision,
            requestId,
            items.Select(item => item.Index).ToArray());
        try
        {
            _pendingPreviewTextMeasurement = pending;
            core.PostWebMessageAsJson(
                _previewPageMessageBuilder.BuildTextMeasurementMessage(
                    token,
                    sourceRevision,
                    requestId,
                    items));
        }
        catch (InvalidOperationException)
        {
            _pendingPreviewTextMeasurement = null;
        }
    }

    private void OnVisualPreviewReset()
    {
        _activePreviewVisualDocument = null;
        _activePreviewSourceRevision = null;
        _visiblePreviewVisualDocument = null;
        _visiblePreviewSourceRevision = null;
        _visualEditGesture = null;
        _visualResizeGesture = null;
        _visualSelectionBridgeId = null;
        _consumedVisualResizeGestureIds.Clear();
        _visualResizeGestureIdOrder.Clear();
        _pendingPreviewTextMeasurement = null;
    }

    private string GetSelectedElementName()
    {
        return _viewModel.Inspector.SelectedElement?.Element.Name
            ?? "Element";
    }

    private static string FormatDelta(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool IsSupportedVisualDelta(
        double deltaX,
        double deltaY)
    {
        return double.IsFinite(deltaX)
            && double.IsFinite(deltaY)
            && Math.Abs(deltaX) <= 1_000_000
            && Math.Abs(deltaY) <= 1_000_000;
    }

    private sealed record VisualEditGesture(
        string GestureId,
        long SourceRevision,
        SvgElementIdentity ElementIdentity,
        SvgVisualPoint StartViewportPoint,
        SvgVisualPoint StartSvgPoint,
        double DeltaX,
        double DeltaY,
        bool HasMoved);

    private sealed record VisualResizeGesture(
        string GestureId,
        string SelectionId,
        long SourceRevision,
        SvgElementIdentity ElementIdentity,
        SvgResizeHandle Handle,
        SvgVisualShapeGeometry PreviewGeometry);
}
