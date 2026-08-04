using System.Collections.ObjectModel;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.ViewModels;

public sealed class DocumentInspectorViewModel : ObservableObject
{
    private readonly Dictionary<string, SvgElementViewModel> _elementsByPath =
        new(StringComparer.Ordinal);
    private SvgDocumentIndex? _documentIndex;
    private SvgElementViewModel? _selectedElement;
    private bool _hasIndex;
    private bool _hasSelection;
    private string _stateTitle = "Indexing source";
    private string _stateMessage = "The element tree will appear after secure SVG validation.";
    private string _selectedElementSummary = "No element selected";
    private string _selectionAdvisory = string.Empty;
    private IReadOnlyList<string> _fontFamilySuggestions = [];
    private readonly SvgOpacityService _opacityService = new();
    private SvgOpacityViewModel? _opacity;
    private string? _source;

    public ObservableCollection<SvgElementViewModel> Roots { get; } = [];

    public ObservableCollection<SvgPropertyViewModel> Properties { get; } = [];

    public SvgDocumentIndex? DocumentIndex => _documentIndex;

    public SvgElementViewModel? SelectedElement => _selectedElement;

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

    public void Load(
        SvgDocumentIndex documentIndex,
        SvgElementIdentity? preferredSelection,
        InspectorSelectionOrigin selectionOrigin =
            InspectorSelectionOrigin.InspectorRestore,
        string? source = null)
    {
        ArgumentNullException.ThrowIfNull(documentIndex);

        _documentIndex = documentIndex;
        _source = source;
        Roots.Clear();
        Properties.Clear();
        _elementsByPath.Clear();

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
        Properties.Clear();
        _elementsByPath.Clear();
        _selectedElement = null;
        Opacity = null;
        HasIndex = false;
        HasSelection = false;
        StateTitle = "Source cannot be indexed";
        StateMessage = message;
        SelectedElementSummary = "No element selected";
        SelectionAdvisory = string.Empty;
        OnPropertyChanged(nameof(DocumentIndex));
        OnPropertyChanged(nameof(SelectedElement));
    }

    public SvgElementViewModel? FindViewModel(SvgElementNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _elementsByPath.GetValueOrDefault(node.StructuralPath);
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

    private void SelectElementCore(
        SvgElementViewModel? element,
        InspectorSelectionOrigin? selectionOrigin)
    {
        if (ReferenceEquals(_selectedElement, element))
        {
            return;
        }

        if (_selectedElement is not null)
        {
            _selectedElement.SetSelected(
                isSelected: false,
                selectionOrigin ?? InspectorSelectionOrigin.InspectorRestore);
        }

        _selectedElement = element;
        Properties.Clear();
        Opacity = null;
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
            HasSelection = true;
            SelectedElementSummary = element.Element.DisplayLabel;

            SvgOpacityControlState opacityState = _opacityService.Analyze(
                _documentIndex!,
                element.Element,
                _source);
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
                        : null));
            }
        }

        OnPropertyChanged(nameof(SelectedElement));
    }

    private SvgElementViewModel CreateElementViewModel(
        SvgElementNode element,
        SvgElementViewModel? parent = null)
    {
        SvgElementViewModel viewModel = new(element, parent);
        _elementsByPath.Add(element.StructuralPath, viewModel);
        foreach (SvgElementNode child in element.Children)
        {
            viewModel.Children.Add(CreateElementViewModel(child, viewModel));
        }

        return viewModel;
    }
}
