using System.Collections.ObjectModel;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.ViewModels;

public sealed class DocumentInspectorViewModel : ObservableObject
{
    private readonly Dictionary<string, SvgElementViewModel> _elementsByPath =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgLayerViewModel> _layersByPath =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _layerExpansionById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _structureExpansionById =
        new(StringComparer.Ordinal);
    private readonly SvgLayerWorkspaceService _layerWorkspaceService = new();
    private SvgDocumentIndex? _documentIndex;
    private SvgElementViewModel? _selectedElement;
    private SvgLayerViewModel? _selectedLayer;
    private bool _hasIndex;
    private bool _hasSelection;
    private string _stateTitle = "Indexing source";
    private string _stateMessage = "The element tree will appear after secure SVG validation.";
    private string _selectedElementSummary = "No element selected";
    private string _selectionAdvisory = string.Empty;
    private IReadOnlyList<string> _fontFamilySuggestions = [];
    private readonly SvgOpacityService _opacityService = new();
    private readonly SvgLayerOrderService _layerOrderService = new();
    private SvgOpacityViewModel? _opacity;
    private string? _source;
    private SvgLayerPositionInfo? _layerPosition;
    private SvgVisualDocument? _visualDocument;

    public ObservableCollection<SvgElementViewModel> Roots { get; } = [];

    public ObservableCollection<SvgLayerViewModel> LayerRoots { get; } = [];

    public ObservableCollection<SvgPropertyViewModel> Properties { get; } = [];

    public SvgDocumentIndex? DocumentIndex => _documentIndex;

    public SvgElementViewModel? SelectedElement => _selectedElement;

    public SvgLayerViewModel? SelectedLayer => _selectedLayer;

    public SvgOpacityViewModel? Opacity
    {
        get => _opacity;
        private set
        {
            if (SetProperty(ref _opacity, value))
            {
                OnPropertyChanged(nameof(HasOpacityControl));
            }
        }
    }

    public bool HasOpacityControl => Opacity is not null;

    public SvgLayerPositionInfo? LayerPosition
    {
        get => _layerPosition;
        private set
        {
            if (SetProperty(ref _layerPosition, value))
            {
                OnPropertyChanged(nameof(HasLayerPosition));
            }
        }
    }

    public bool HasLayerPosition => LayerPosition?.IsEligible == true;

    public bool HasIndex
    {
        get => _hasIndex;
        private set => SetProperty(ref _hasIndex, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public string StateTitle
    {
        get => _stateTitle;
        private set => SetProperty(ref _stateTitle, value);
    }

    public string StateMessage
    {
        get => _stateMessage;
        private set => SetProperty(ref _stateMessage, value);
    }

    public string SelectedElementSummary
    {
        get => _selectedElementSummary;
        private set => SetProperty(ref _selectedElementSummary, value);
    }

    public string SelectionAdvisory
    {
        get => _selectionAdvisory;
        private set => SetProperty(ref _selectionAdvisory, value);
    }

    public SvgElementIdentity? CaptureSelectionIdentity() =>
        _selectedElement?.Element.Identity;

    public void BeginDocumentSession()
    {
        _layerWorkspaceService.BeginDocumentSession();
        _layerExpansionById.Clear();
        _structureExpansionById.Clear();
    }

    public void SetFontFamilySuggestions(
        IReadOnlyList<string> fontFamilySuggestions)
    {
        _fontFamilySuggestions = fontFamilySuggestions
            ?? throw new ArgumentNullException(nameof(fontFamilySuggestions));
    }

    public void SetSelectionAdvisory(string? message)
    {
        SelectionAdvisory = message ?? string.Empty;
    }

    public void RefreshLayerPresentation(SvgVisualDocument visualDocument)
    {
        ArgumentNullException.ThrowIfNull(visualDocument);
        _visualDocument = visualDocument;
        RefreshLayers();
    }

    public void Load(
        SvgDocumentIndex documentIndex,
        SvgElementIdentity? preferredSelection,
        InspectorSelectionOrigin selectionOrigin =
            InspectorSelectionOrigin.InspectorRestore,
        string? source = null,
        SvgVisualDocument? visualDocument = null)
    {
        ArgumentNullException.ThrowIfNull(documentIndex);

        CaptureStructureExpansion();
        CaptureLayerExpansion();
        _documentIndex = documentIndex;
        _source = source;
        _visualDocument = visualDocument;
        Roots.Clear();
        LayerRoots.Clear();
        Properties.Clear();
        _elementsByPath.Clear();
        _layersByPath.Clear();

        BuildLayerViewModels();
        foreach (SvgElementNode root in documentIndex.Roots)
        {
            Roots.Add(CreateElementViewModel(root));
        }

        HasIndex = true;
        StateTitle = "Document indexed";
        StateMessage = $"{documentIndex.Elements.Count} SVG element(s)";

        SvgElementNode? selectedNode = preferredSelection is null
            ? documentIndex.Roots.FirstOrDefault()
            : documentIndex.FindBestMatch(preferredSelection);
        SelectNode(selectedNode, selectionOrigin);
        OnPropertyChanged(nameof(DocumentIndex));
    }

    public void ShowUnavailable(string message)
    {
        _documentIndex = null;
        _source = null;
        Roots.Clear();
        LayerRoots.Clear();
        Properties.Clear();
        _elementsByPath.Clear();
        _layersByPath.Clear();
        _selectedElement = null;
        _selectedLayer = null;
        Opacity = null;
        LayerPosition = null;
        HasIndex = false;
        HasSelection = false;
        StateTitle = "Source cannot be indexed";
        StateMessage = message;
        SelectedElementSummary = "No element selected";
        SelectionAdvisory = string.Empty;
        OnPropertyChanged(nameof(DocumentIndex));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(SelectedLayer));
    }

    public SvgElementViewModel? FindViewModel(SvgElementNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _elementsByPath.GetValueOrDefault(node.StructuralPath);
    }

    public SvgLayerViewModel? FindLayerViewModel(SvgElementNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        for (SvgElementNode? current = node;
             current is not null;
             current = _documentIndex?.FindParent(current))
        {
            if (_layersByPath.TryGetValue(
                    current.StructuralPath,
                    out SvgLayerViewModel? layer))
            {
                return layer;
            }
        }

        return null;
    }

    public SvgLayerViewModel? FindLayerViewModel(string opaqueId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueId);
        return _layersByPath.Values.FirstOrDefault(layer =>
            layer.OpaqueId.Equals(opaqueId, StringComparison.Ordinal));
    }

    public bool IsElementEffectivelyLocked(SvgElementNode element) =>
        _documentIndex is not null
        && _layerWorkspaceService.IsEffectivelyLocked(
            _documentIndex,
            element);

    public bool ToggleLayerLock(SvgLayerViewModel layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layerWorkspaceService.ToggleLock(layer.OpaqueId))
        {
            return false;
        }

        RefreshLayers();
        return true;
    }

    public bool IsHiddenAttributeOwned(SvgLayerViewModel layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return _layerWorkspaceService.IsHiddenAttributeOwned(layer.OpaqueId);
    }

    public void SetHiddenAttributeOwned(
        string opaqueId,
        bool isOwned)
    {
        _layerWorkspaceService.SetHiddenAttributeOwned(opaqueId, isOwned);
    }

    public void SelectNode(
        SvgElementNode? node,
        InspectorSelectionOrigin origin =
            InspectorSelectionOrigin.SourceCaretSync)
    {
        SvgElementViewModel? viewModel = node is null
            ? null
            : FindViewModel(node);
        SelectElementCore(viewModel, origin);
    }

    public void AcceptTreeSelection(SvgElementViewModel? element)
    {
        SelectElementCore(element, selectionOrigin: null);
    }

    public void AcceptLayerSelection(SvgLayerViewModel? layer)
    {
        SelectElementCore(
            layer is null ? null : FindViewModel(layer.Element),
            selectionOrigin: null);
    }

    private void SelectElementCore(
        SvgElementViewModel? element,
        InspectorSelectionOrigin? selectionOrigin)
    {
        SvgLayerViewModel? layer = element is null
            ? null
            : FindLayerViewModel(element.Element);
        if (ReferenceEquals(_selectedElement, element)
            && ReferenceEquals(_selectedLayer, layer))
        {
            return;
        }

        if (_selectedElement is not null)
        {
            _selectedElement.SetSelected(
                isSelected: false,
                selectionOrigin ?? InspectorSelectionOrigin.InspectorRestore);
        }
        if (_selectedLayer is not null)
        {
            _selectedLayer.SetSelected(
                isSelected: false,
                selectionOrigin ?? InspectorSelectionOrigin.InspectorRestore);
        }

        _selectedElement = element;
        _selectedLayer = layer;
        Properties.Clear();
        Opacity = null;
        LayerPosition = null;
        SelectionAdvisory = string.Empty;

        if (element is null)
        {
            HasSelection = false;
            SelectedElementSummary = "No element selected";
        }
        else
        {
            for (SvgElementViewModel? ancestor = element.Parent;
                 ancestor is not null;
                 ancestor = ancestor.Parent)
            {
                ancestor.IsExpanded = true;
            }

            if (!element.IsSelected)
            {
                if (selectionOrigin is InspectorSelectionOrigin origin)
                {
                    element.SetSelected(isSelected: true, origin);
                }
                else
                {
                    element.IsSelected = true;
                }
            }
            if (layer is not null)
            {
                for (SvgLayerViewModel? ancestor = layer.Parent;
                     ancestor is not null;
                     ancestor = ancestor.Parent)
                {
                    ancestor.IsExpanded = true;
                }
                if (!layer.IsSelected)
                {
                    if (selectionOrigin is InspectorSelectionOrigin origin)
                    {
                        layer.SetSelected(isSelected: true, origin);
                    }
                    else
                    {
                        layer.IsSelected = true;
                    }
                }
            }
            HasSelection = true;
            SelectedElementSummary = element.Element.DisplayLabel;
            SvgLayerPositionInfo layerPosition =
                _layerOrderService.GetPositionInfo(
                    _documentIndex!,
                    element.Element);
            if (layerPosition.IsEligible)
            {
                LayerPosition = layerPosition;
            }

            SvgOpacityControlState opacityState = _opacityService.Analyze(
                _documentIndex!,
                element.Element,
                _source);
            bool isLocked = IsElementEffectivelyLocked(element.Element);
            if (opacityState.IsVisible && isLocked)
            {
                opacityState = opacityState with
                {
                    IsEnabled = false,
                    UnavailableReason =
                        "Unlock this layer or its parent group to edit opacity."
                };
            }
            if (opacityState.IsVisible)
            {
                Opacity = new SvgOpacityViewModel(element.Element, opacityState);
            }

            foreach (SvgPropertyDefinition definition in
                     SvgPropertySchema.GetProperties(element.Element.Name)
                         .Where(definition => !definition.Name.Equals(
                             "opacity",
                             StringComparison.Ordinal)))
            {
                Properties.Add(new SvgPropertyViewModel(
                    element.Element,
                    definition,
                    element.Element.FindAttribute(definition.Name),
                    definition.UsesFontFamilySuggestions
                        ? _fontFamilySuggestions
                        : null,
                    isSessionLocked: isLocked));
            }
        }

        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(SelectedLayer));
    }

    private SvgElementViewModel CreateElementViewModel(
        SvgElementNode element,
        SvgElementViewModel? parent = null)
    {
        SvgElementViewModel viewModel = new(element, parent);
        string expansionKey = GetStructureExpansionKey(element);
        if (_structureExpansionById.TryGetValue(
                expansionKey,
                out bool isExpanded))
        {
            viewModel.IsExpanded = isExpanded;
        }
        _elementsByPath.Add(element.StructuralPath, viewModel);
        foreach (SvgElementNode child in element.Children)
        {
            viewModel.Children.Add(CreateElementViewModel(child, viewModel));
        }

        return viewModel;
    }

    private void RefreshLayers()
    {
        if (_documentIndex is null)
        {
            return;
        }

        SvgElementViewModel? selectedElement = _selectedElement;
        CaptureLayerExpansion();
        LayerRoots.Clear();
        _layersByPath.Clear();
        BuildLayerViewModels();
        SelectElementCore(
            selectedElement,
            InspectorSelectionOrigin.InspectorRestore);
    }

    private void BuildLayerViewModels()
    {
        SvgLayerWorkspace workspace = _layerWorkspaceService.Build(
            _documentIndex!,
            _source ?? string.Empty,
            _visualDocument);
        foreach (SvgLayerItem root in workspace.Roots)
        {
            LayerRoots.Add(CreateLayerViewModel(root));
        }
    }

    private SvgLayerViewModel CreateLayerViewModel(
        SvgLayerItem item,
        SvgLayerViewModel? parent = null)
    {
        bool isExpanded = _layerExpansionById.GetValueOrDefault(
            item.OpaqueId,
            item.Element.Depth < 2);
        SvgLayerViewModel viewModel = new(item, parent, isExpanded);
        _layersByPath.Add(item.Element.StructuralPath, viewModel);
        foreach (SvgLayerItem child in item.Children)
        {
            viewModel.Children.Add(CreateLayerViewModel(child, viewModel));
        }

        return viewModel;
    }

    private void CaptureLayerExpansion()
    {
        foreach (SvgLayerViewModel layer in EnumerateLayers(LayerRoots))
        {
            _layerExpansionById[layer.OpaqueId] = layer.IsExpanded;
        }
    }

    private void CaptureStructureExpansion()
    {
        foreach (SvgElementViewModel element in EnumerateElements(Roots))
        {
            _structureExpansionById[GetStructureExpansionKey(element.Element)] =
                element.IsExpanded;
        }
    }

    private string GetStructureExpansionKey(SvgElementNode element)
    {
        if (_layersByPath.TryGetValue(
                element.StructuralPath,
                out SvgLayerViewModel? layer))
        {
            return $"layer:{layer.OpaqueId}";
        }

        return !string.IsNullOrWhiteSpace(element.Id)
            ? $"id:{element.Name}:{element.Id}"
            : $"path:{element.Name}:{element.StructuralPath}";
    }

    private static IEnumerable<SvgLayerViewModel> EnumerateLayers(
        IEnumerable<SvgLayerViewModel> roots)
    {
        foreach (SvgLayerViewModel root in roots)
        {
            yield return root;
            foreach (SvgLayerViewModel child in EnumerateLayers(root.Children))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<SvgElementViewModel> EnumerateElements(
        IEnumerable<SvgElementViewModel> roots)
    {
        foreach (SvgElementViewModel root in roots)
        {
            yield return root;
            foreach (SvgElementViewModel child in EnumerateElements(root.Children))
            {
                yield return child;
            }
        }
    }
}
