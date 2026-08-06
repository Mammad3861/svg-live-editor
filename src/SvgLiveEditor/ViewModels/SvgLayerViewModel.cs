using System.Collections.ObjectModel;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.ViewModels;

public sealed class SvgLayerViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isExpanded;
    private bool _isDropBefore;
    private bool _isDropAfter;
    private bool _isDropInside;
    private InspectorSelectionOrigin? _pendingSelectionOrigin;

    public SvgLayerViewModel(
        SvgLayerItem item,
        SvgLayerViewModel? parent,
        bool isExpanded)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Parent = parent;
        _isExpanded = isExpanded;
        Children = [];
    }

    public SvgLayerItem Item { get; }

    public SvgElementNode Element => Item.Element;

    public SvgLayerViewModel? Parent { get; }

    public ObservableCollection<SvgLayerViewModel> Children { get; }

    public string OpaqueId => Item.OpaqueId;

    public string Label => Item.Label;

    public string TypeLabel => Item.IsGroup ? "Group" : Item.Element.Name;

    public string TypeIcon => Item.Element.Name switch
    {
        "g" => "▣",
        "rect" => "□",
        "circle" => "○",
        "ellipse" => "⬭",
        "line" => "╱",
        "text" => "T",
        "path" => "⌁",
        "polygon" => "△",
        "polyline" => "⌁",
        _ => "◇"
    };

    public bool IsGroup => Item.IsGroup;

    public bool IsInspectionOnly => Item.IsInspectionOnly;

    public bool IsLocked => Item.IsLocked;

    public bool IsEffectivelyLocked => Item.IsEffectivelyLocked;

    public bool CanToggleLock => IsLocked || !IsEffectivelyLocked;

    public bool IsVisible => Item.Visibility.IsVisible;

    public bool CanToggleVisibility => Item.Visibility.CanToggle;

    public string VisibilityHelp => Item.Visibility.CanToggle
        ? IsVisible
            ? $"Hide {Label} by adding a standard display attribute"
            : $"Show {Label} by removing this session-owned hidden state"
        : Item.Visibility.UnavailableReason
            ?? "Visibility cannot be changed safely.";

    public string VisibilityAutomationName => CanToggleVisibility
        ? IsVisible
            ? $"Hide {Label}"
            : $"Show {Label}"
        : $"Visibility unavailable for {Label}";

    public string LockHelp => IsLocked
        ? $"Unlock {Label} for visual editing in this session"
        : IsEffectivelyLocked
            ? $"{Label} is locked by a parent group"
            : $"Lock {Label} against visual edits in this session";

    public string LockAutomationName => IsLocked
        ? $"Unlock {Label}"
        : IsEffectivelyLocked
            ? $"{Label} locked by a parent group"
            : $"Lock {Label}";

    public string EditabilityHelp => IsInspectionOnly
        ? "Inspection-only artwork: it can be selected and reordered, but its current geometry is not visually movable."
        : IsGroup
            ? "Group: reordered as one paint-order unit; children have their own order."
            : "Visually editable artwork.";

    public string RowHelp =>
        $"{EditabilityHelp} {VisibilityHelp} {LockHelp}";

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

    public bool IsDropBefore
    {
        get => _isDropBefore;
        set => SetProperty(ref _isDropBefore, value);
    }

    public bool IsDropAfter
    {
        get => _isDropAfter;
        set => SetProperty(ref _isDropAfter, value);
    }

    public bool IsDropInside
    {
        get => _isDropInside;
        set => SetProperty(ref _isDropInside, value);
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
