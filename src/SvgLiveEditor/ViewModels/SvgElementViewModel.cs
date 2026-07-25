using System.Collections.ObjectModel;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgElementViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isExpanded;

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
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
