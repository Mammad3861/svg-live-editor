using System.Collections.ObjectModel;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgElementViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isExpanded;
    private InspectorSelectionOrigin? _pendingSelectionOrigin;

    public SvgElementViewModel(
        SvgElementNode element,
        SvgElementViewModel? parent)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Parent = parent;
        Children = [];
        _isExpanded = element.Depth < 2;
    }

    public SvgElementNode Element { get; }

    public SvgElementViewModel? Parent { get; }

    public string Label => Element.DisplayLabel;

    public ObservableCollection<SvgElementViewModel> Children { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!value)
            {
                _pendingSelectionOrigin = null;
            }

            SetProperty(ref _isSelected, value);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public void SetSelected(
        bool isSelected,
        InspectorSelectionOrigin origin)
    {
        _pendingSelectionOrigin = isSelected ? origin : null;
        IsSelected = isSelected;
    }

    public InspectorSelectionOrigin? ConsumePendingSelectionOrigin()
    {
        InspectorSelectionOrigin? origin = _pendingSelectionOrigin;
        _pendingSelectionOrigin = null;
        return origin;
    }
}
