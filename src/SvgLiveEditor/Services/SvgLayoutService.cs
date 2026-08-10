using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgLayoutService
{
    private const double Epsilon = 0.0000005;
    private readonly SvgMultiVisualMoveService _moveService = new();

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        SvgLayoutCommand command,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(elements);
        string? error = Validate(
            document,
            elements,
            command,
            isEffectivelyLocked);
        if (error is not null)
        {
            return SvgAttributeEditResult.Invalid(error);
        }

        IReadOnlyList<SvgVisualMoveDelta> movements = command switch
        {
            SvgLayoutCommand.AlignLeft
                or SvgLayoutCommand.AlignHorizontalCenters
                or SvgLayoutCommand.AlignRight
                or SvgLayoutCommand.AlignTop
                or SvgLayoutCommand.AlignVerticalCenters
                or SvgLayoutCommand.AlignBottom =>
                CreateAlignmentMovements(elements, command),
            SvgLayoutCommand.DistributeHorizontally
                or SvgLayoutCommand.DistributeVertically =>
                CreateDistributionMovements(elements, command),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        SvgVisualMoveDelta[] changed = movements
            .Where(item => Math.Abs(item.DeltaX) >= Epsilon
                || Math.Abs(item.DeltaY) >= Epsilon)
            .ToArray();
        return changed.Length == 0
            ? SvgAttributeEditResult.Success(edit: null)
            : _moveService.CreateEdit(source, changed);
    }

    public SvgAuthoringAvailability GetAvailability(
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        SvgLayoutCommand command,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        string? error = Validate(
            document,
            elements,
            command,
            isEffectivelyLocked);
        return error is null
            ? new SvgAuthoringAvailability(true)
            : new SvgAuthoringAvailability(false, error);
    }

    private static string? Validate(
        SvgDocumentIndex document,
        IReadOnlyList<SvgVisualElement> elements,
        SvgLayoutCommand command,
        Func<SvgElementNode, bool>? isEffectivelyLocked)
    {
        int minimum = command is SvgLayoutCommand.DistributeHorizontally
            or SvgLayoutCommand.DistributeVertically
                ? 3
                : 2;
        if (elements.Count < minimum
            || elements.Count > SvgMultiSelectionService.MaximumSelectionCount
            || elements.Select(item => item.SourceElement.Identity)
                .Distinct().Count() != elements.Count)
        {
            return command is SvgLayoutCommand.DistributeHorizontally
                or SvgLayoutCommand.DistributeVertically
                    ? "Select 3 to 128 distinct elements to distribute."
                    : "Select 2 to 128 distinct elements to align.";
        }
        if (elements.Any(element =>
                !element.IsMovable
                || element.Geometry is null
                || isEffectivelyLocked?.Invoke(element.SourceElement) == true))
        {
            return "Every selected element must be measurable, movable, and unlocked.";
        }

        SvgElementNode? parent = document.FindParent(elements[0].SourceElement);
        if (parent is null
            || elements.Any(element => !ReferenceEquals(
                document.FindParent(element.SourceElement),
                parent)))
        {
            return "Layout commands require selected elements under one compatible parent coordinate system.";
        }
        return null;
    }

    private static IReadOnlyList<SvgVisualMoveDelta>
        CreateAlignmentMovements(
            IReadOnlyList<SvgVisualElement> elements,
            SvgLayoutCommand command)
    {
        SvgVisualBounds[] bounds = elements
            .Select(element => element.Geometry!.Bounds)
            .ToArray();
        double left = bounds.Min(item => item.Left);
        double right = bounds.Max(item => item.Right);
        double top = bounds.Min(item => item.Top);
        double bottom = bounds.Max(item => item.Bottom);
        double horizontalCenter = (left + right) / 2;
        double verticalCenter = (top + bottom) / 2;

        return elements.Select((element, index) =>
        {
            SvgVisualBounds item = bounds[index];
            double deltaX = command switch
            {
                SvgLayoutCommand.AlignLeft => left - item.Left,
                SvgLayoutCommand.AlignHorizontalCenters =>
                    horizontalCenter - ((item.Left + item.Right) / 2),
                SvgLayoutCommand.AlignRight => right - item.Right,
                _ => 0
            };
            double deltaY = command switch
            {
                SvgLayoutCommand.AlignTop => top - item.Top,
                SvgLayoutCommand.AlignVerticalCenters =>
                    verticalCenter - ((item.Top + item.Bottom) / 2),
                SvgLayoutCommand.AlignBottom => bottom - item.Bottom,
                _ => 0
            };
            return new SvgVisualMoveDelta(element, deltaX, deltaY);
        }).ToArray();
    }

    private static IReadOnlyList<SvgVisualMoveDelta>
        CreateDistributionMovements(
            IReadOnlyList<SvgVisualElement> elements,
            SvgLayoutCommand command)
    {
        bool horizontal = command == SvgLayoutCommand.DistributeHorizontally;
        SvgVisualElement[] ordered = elements
            .OrderBy(element => horizontal
                ? element.Geometry!.Bounds.Left
                : element.Geometry!.Bounds.Top)
            .ThenBy(element => element.SourceElement.FullSpan.Start)
            .ToArray();
        double outerStart = horizontal
            ? ordered[0].Geometry!.Bounds.Left
            : ordered[0].Geometry!.Bounds.Top;
        double outerEnd = horizontal
            ? ordered[^1].Geometry!.Bounds.Right
            : ordered[^1].Geometry!.Bounds.Bottom;
        double totalSize = ordered.Sum(element => horizontal
            ? element.Geometry!.Bounds.Width
            : element.Geometry!.Bounds.Height);
        double gap = (outerEnd - outerStart - totalSize)
            / (ordered.Length - 1);
        double cursor = outerStart;
        List<SvgVisualMoveDelta> movements = [];
        for (int index = 0; index < ordered.Length; index++)
        {
            SvgVisualElement element = ordered[index];
            SvgVisualBounds bounds = element.Geometry!.Bounds;
            double current = horizontal ? bounds.Left : bounds.Top;
            double delta = index is 0 || index == ordered.Length - 1
                ? 0
                : cursor - current;
            movements.Add(new SvgVisualMoveDelta(
                element,
                horizontal ? delta : 0,
                horizontal ? 0 : delta));
            cursor += (horizontal ? bounds.Width : bounds.Height) + gap;
        }
        return movements;
    }
}

