using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualHitTestService
{
    public SvgVisualElement? HitTest(
        SvgVisualDocument document,
        SvgMappedPreviewPoint pointer) =>
        HitTestDetailed(document, pointer).Element;

    public SvgVisualHitTestResult HitTestDetailed(
        SvgVisualDocument document,
        SvgMappedPreviewPoint pointer)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!double.IsFinite(pointer.Point.X)
            || !double.IsFinite(pointer.Point.Y)
            || !double.IsFinite(pointer.HitTolerance)
            || pointer.HitTolerance < 0)
        {
            return SvgVisualHitTestResult.None;
        }

        for (int index = document.Elements.Count - 1; index >= 0; index--)
        {
            SvgVisualElement element = document.Elements[index];
            if (element.Geometry is SvgVisualShapeGeometry geometry
                && !Contains(geometry, pointer))
            {
                continue;
            }
            if (element.IsSelectable)
            {
                return new SvgVisualHitTestResult(element, null);
            }
            if (element.BlocksLowerVisualHits)
            {
                return new SvgVisualHitTestResult(null, element);
            }
        }

        return SvgVisualHitTestResult.None;
    }

    private static bool Contains(
        SvgVisualShapeGeometry geometry,
        SvgMappedPreviewPoint pointer)
    {
        SvgVisualBounds bounds = geometry.Bounds;
        double tolerance = pointer.HitTolerance;
        double x = pointer.Point.X;
        double y = pointer.Point.Y;
        return geometry.Kind switch
        {
            SvgVisualElementKind.Rect
                or SvgVisualElementKind.Text
                or SvgVisualElementKind.Unsupported =>
                x >= bounds.Left - tolerance
                && x <= bounds.Right + tolerance
                && y >= bounds.Top - tolerance
                && y <= bounds.Bottom + tolerance,
            SvgVisualElementKind.Circle
                or SvgVisualElementKind.Ellipse =>
                IsInsideEllipse(bounds, x, y, tolerance),
            SvgVisualElementKind.Line =>
                DistanceToSegment(
                    x,
                    y,
                    geometry.X1,
                    geometry.Y1,
                    geometry.X2,
                    geometry.Y2) <= tolerance,
            _ => false
        };
    }

    private static bool IsInsideEllipse(
        SvgVisualBounds bounds,
        double x,
        double y,
        double tolerance)
    {
        double radiusX = (bounds.Width / 2) + tolerance;
        double radiusY = (bounds.Height / 2) + tolerance;
        if (radiusX <= 0 || radiusY <= 0)
        {
            return false;
        }

        double centerX = (bounds.Left + bounds.Right) / 2;
        double centerY = (bounds.Top + bounds.Bottom) / 2;
        double normalizedX = (x - centerX) / radiusX;
        double normalizedY = (y - centerY) / radiusY;
        return (normalizedX * normalizedX)
            + (normalizedY * normalizedY) <= 1;
    }

    private static double DistanceToSegment(
        double x,
        double y,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        double segmentX = x2 - x1;
        double segmentY = y2 - y1;
        double lengthSquared = (segmentX * segmentX)
            + (segmentY * segmentY);
        if (lengthSquared <= double.Epsilon)
        {
            return Math.Sqrt(
                ((x - x1) * (x - x1))
                + ((y - y1) * (y - y1)));
        }

        double projection = Math.Clamp(
            (((x - x1) * segmentX) + ((y - y1) * segmentY))
            / lengthSquared,
            0,
            1);
        double nearestX = x1 + (projection * segmentX);
        double nearestY = y1 + (projection * segmentY);
        double deltaX = x - nearestX;
        double deltaY = y - nearestY;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
