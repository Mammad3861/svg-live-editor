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
    private VisualEditGesture? _visualEditGesture;
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
                ?? $"Visual editing is not available for {blocker.SourceElement.Name} elements in v0.6.0.");
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

    private static bool HasVisualGestureModifier(
        PreviewVisualPointerMessage pointer)
    {
        return pointer.ControlHeld
            || pointer.ShiftHeld
            || pointer.AltHeld
            || pointer.MetaHeld
            || pointer.SpaceHeld;
    }

    private void SelectVisualElement(SvgVisualElement element)
    {
        _visualSelectionIdentity = element.SourceElement.Identity;
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
    }

    private void ClearVisualSelection()
    {
        _visualSelectionIdentity = null;
        _visualEditGesture = null;
        _viewModel.Inspector.SelectNode(
            null,
            InspectorSelectionOrigin.PreviewNavigation);
        PostVisualSelection(selection: null);
    }

    private void SynchronizeVisualSelectionFromInspector(
        bool announce = false)
    {
        SvgElementIdentity? identity =
            _viewModel.Inspector.SelectedElement?.Element.Identity;
        _visualSelectionIdentity = identity;
        if (identity is null)
        {
            PostVisualSelection(selection: null);
            return;
        }

        SvgVisualElement? visualElement =
            _lastValidVisualDocument?.FindElement(identity);
        if (visualElement is null || !visualElement.IsMovable)
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
    }

    private void ShowVisualSelection(
        double deltaX = 0,
        double deltaY = 0)
    {
        if (_visualSelectionIdentity
                is not SvgElementIdentity identity
            || _visiblePreviewVisualDocument?.FindElement(identity)
                is not SvgVisualElement
                {
                    Geometry: SvgVisualShapeGeometry geometry
                } element)
        {
            PostVisualSelection(selection: null);
            return;
        }

        PostVisualSelection(new PreviewVisualSelection(
            element.Kind,
            geometry,
            deltaX,
            deltaY));
    }

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
        if (_visualEditGesture is null)
        {
            return;
        }

        _visualEditGesture = null;
        ShowVisualSelection();
        if (!string.IsNullOrWhiteSpace(status))
        {
            _viewModel.SetOperationStatus(status);
        }
    }

    private void OnVisualSourceChanged()
    {
        CancelVisualEditGesture();
        PostVisualSelection(selection: null);
    }

    private void OnVisualDocumentLoaded()
    {
        _visualEditGesture = null;
        _visualSelectionIdentity = null;
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
    }

    private void OnVisualPreviewNavigationStarted(
        SvgVisualDocument visualDocument,
        long sourceRevision)
    {
        CancelVisualEditGesture();
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
}
