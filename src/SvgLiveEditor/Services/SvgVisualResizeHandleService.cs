using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualResizeHandleService
{
    private static readonly SvgResizeHandle[] BoxHandles =
    [
        SvgResizeHandle.TopLeft,
        SvgResizeHandle.Top,
        SvgResizeHandle.TopRight,
        SvgResizeHandle.Right,
        SvgResizeHandle.BottomRight,
        SvgResizeHandle.Bottom,
        SvgResizeHandle.BottomLeft,
        SvgResizeHandle.Left
    ];

    private static readonly SvgResizeHandle[] CircleHandles =
    [
        SvgResizeHandle.Top,
        SvgResizeHandle.Right,
        SvgResizeHandle.Bottom,
        SvgResizeHandle.Left
    ];

    private static readonly SvgResizeHandle[] LineHandles =
    [
        SvgResizeHandle.Start,
        SvgResizeHandle.End
    ];

    public IReadOnlyList<SvgResizeHandleDefinition> Create(
        SvgVisualElement element,
        SvgVisualShapeGeometry? geometry = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!element.IsResizable
            || (geometry ?? element.Geometry)
                is not SvgVisualShapeGeometry shape)
        {
            return [];
        }

        return HandlesFor(element.Kind)
            .Select(handle => new SvgResizeHandleDefinition(
                handle,
                GetPoint(shape, handle)))
            .ToArray();
    }

    public bool IsAllowed(SvgVisualElement element, SvgResizeHandle handle)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.IsResizable && HandlesFor(element.Kind).Contains(handle);
    }

    public static bool IsCorner(SvgResizeHandle handle) =>
        handle is SvgResizeHandle.TopLeft
            or SvgResizeHandle.TopRight
            or SvgResizeHandle.BottomRight
            or SvgResizeHandle.BottomLeft;

    public static string ToWireName(SvgResizeHandle handle) => handle switch
    {
        SvgResizeHandle.TopLeft => "top-left",
        SvgResizeHandle.Top => "top",
        SvgResizeHandle.TopRight => "top-right",
        SvgResizeHandle.Right => "right",
        SvgResizeHandle.BottomRight => "bottom-right",
        SvgResizeHandle.Bottom => "bottom",
        SvgResizeHandle.BottomLeft => "bottom-left",
        SvgResizeHandle.Left => "left",
        SvgResizeHandle.Start => "start",
        SvgResizeHandle.End => "end",
        _ => throw new ArgumentOutOfRangeException(nameof(handle))
    };

    public static bool TryParseWireName(
        string? value,
        out SvgResizeHandle handle)
    {
        handle = value switch
        {
            "top-left" => SvgResizeHandle.TopLeft,
            "top" => SvgResizeHandle.Top,
            "top-right" => SvgResizeHandle.TopRight,
            "right" => SvgResizeHandle.Right,
            "bottom-right" => SvgResizeHandle.BottomRight,
            "bottom" => SvgResizeHandle.Bottom,
            "bottom-left" => SvgResizeHandle.BottomLeft,
            "left" => SvgResizeHandle.Left,
            "start" => SvgResizeHandle.Start,
            "end" => SvgResizeHandle.End,
            _ => default
        };
        return value is "top-left" or "top" or "top-right" or "right"
            or "bottom-right" or "bottom" or "bottom-left" or "left"
            or "start" or "end";
    }

    private static IReadOnlyList<SvgResizeHandle> HandlesFor(
        SvgVisualElementKind kind) => kind switch
    {
        SvgVisualElementKind.Rect or SvgVisualElementKind.Ellipse =>
            BoxHandles,
        SvgVisualElementKind.Circle => CircleHandles,
        SvgVisualElementKind.Line => LineHandles,
        _ => []
    };

    private static SvgVisualPoint GetPoint(
        SvgVisualShapeGeometry geometry,
        SvgResizeHandle handle)
    {
        SvgVisualBounds bounds = geometry.Bounds;
        double centerX = (bounds.Left + bounds.Right) / 2;
        double centerY = (bounds.Top + bounds.Bottom) / 2;
        return handle switch
        {
            SvgResizeHandle.TopLeft => new(bounds.Left, bounds.Top),
            SvgResizeHandle.Top => new(centerX, bounds.Top),
            SvgResizeHandle.TopRight => new(bounds.Right, bounds.Top),
            SvgResizeHandle.Right => new(bounds.Right, centerY),
            SvgResizeHandle.BottomRight => new(bounds.Right, bounds.Bottom),
            SvgResizeHandle.Bottom => new(centerX, bounds.Bottom),
            SvgResizeHandle.BottomLeft => new(bounds.Left, bounds.Bottom),
            SvgResizeHandle.Left => new(bounds.Left, centerY),
            SvgResizeHandle.Start => new(geometry.X1, geometry.Y1),
            SvgResizeHandle.End => new(geometry.X2, geometry.Y2),
            _ => throw new ArgumentOutOfRangeException(nameof(handle))
        };
    }
}
