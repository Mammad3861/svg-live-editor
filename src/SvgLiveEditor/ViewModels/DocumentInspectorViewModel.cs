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

    public ObservableCollection<SvgElementViewModel> Roots { get; } = [];

    public ObservableCollection<SvgPropertyViewModel> Properties { get; } = [];

    public SvgDocumentIndex? DocumentIndex => _documentIndex;

    public SvgElementViewModel? SelectedElement => _selectedElement;

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

    public SvgElementIdentity? CaptureSelectionIdentity() =>
        _selectedElement?.Element.Identity;

    public void Load(
        SvgDocumentIndex documentIndex,
        SvgElementIdentity? preferredSelection)
    {
        ArgumentNullException.ThrowIfNull(documentIndex);

        _documentIndex = documentIndex;
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
        SelectNode(selectedNode);
        OnPropertyChanged(nameof(DocumentIndex));
    }

    public void ShowUnavailable(string message)
    {
        _documentIndex = null;
        Roots.Clear();
        Properties.Clear();
        _elementsByPath.Clear();
        _selectedElement = null;
        HasIndex = false;
        HasSelection = false;
        StateTitle = "Source cannot be indexed";
        StateMessage = message;
        SelectedElementSummary = "No element selected";
        OnPropertyChanged(nameof(DocumentIndex));
        OnPropertyChanged(nameof(SelectedElement));
    }

    public SvgElementViewModel? FindViewModel(SvgElementNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _elementsByPath.GetValueOrDefault(node.StructuralPath);
    }

    public void SelectNode(SvgElementNode? node)
    {
        SvgElementViewModel? viewModel = node is null
            ? null
            : FindViewModel(node);
        SelectElement(viewModel);
    }

    public void SelectElement(SvgElementViewModel? element)
    {
        if (ReferenceEquals(_selectedElement, element))
        {
            return;
        }

        if (_selectedElement is not null)
        {
            _selectedElement.IsSelected = false;
        }

        _selectedElement = element;
        Properties.Clear();

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

            element.IsSelected = true;
            HasSelection = true;
            SelectedElementSummary = element.Element.DisplayLabel;

            foreach (SvgPropertyDefinition definition in
                     SvgPropertySchema.GetProperties(element.Element.Name))
            {
                Properties.Add(new SvgPropertyViewModel(
                    element.Element,
                    definition,
                    element.Element.FindAttribute(definition.Name)));
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
