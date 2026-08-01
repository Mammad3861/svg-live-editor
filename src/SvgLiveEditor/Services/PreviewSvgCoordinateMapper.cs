using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewSvgCoordinateMapper
{
    private const double HitTolerancePixels = 6;

    public bool TryMap(
        SvgVisualViewport viewport,
        PreviewImageMetrics image,
        SvgVisualPoint viewportPoint,
        out SvgMappedPreviewPoint mappedPoint)
    {
        mappedPoint = default;
        if (!IsPositiveFinite(image.Width)
            || !IsPositiveFinite(image.Height)
            || !IsPositiveFinite(viewport.Width)
            || !IsPositiveFinite(viewport.Height)
            || !double.IsFinite(image.Left)
            || !double.IsFinite(image.Top)
            || !double.IsFinite(viewportPoint.X)
            || !double.IsFinite(viewportPoint.Y))
        {
            return false;
        }

        double localX = viewportPoint.X - image.Left;
        double localY = viewportPoint.Y - image.Top;
        if (localX < 0
            || localY < 0
            || localX > image.Width
            || localY > image.Height)
        {
            return false;
        }

        SvgPreserveAspectRatio aspect = viewport.PreserveAspectRatio;
        double userX;
        double userY;
        double tolerance;
        if (aspect.IsNone)
        {
            double unitsPerPixelX = viewport.Width / image.Width;
            double unitsPerPixelY = viewport.Height / image.Height;
            userX = viewport.MinX + (localX * unitsPerPixelX);
            userY = viewport.MinY + (localY * unitsPerPixelY);
            tolerance = HitTolerancePixels
                * Math.Max(unitsPerPixelX, unitsPerPixelY);
        }
        else
        {
            double scaleX = image.Width / viewport.Width;
            double scaleY = image.Height / viewport.Height;
            double scale = aspect.IsSlice
                ? Math.Max(scaleX, scaleY)
                : Math.Min(scaleX, scaleY);
            if (!IsPositiveFinite(scale))
            {
                return false;
            }

            double contentWidth = viewport.Width * scale;
            double contentHeight = viewport.Height * scale;
            double offsetX = (image.Width - contentWidth) * aspect.AlignX;
            double offsetY = (image.Height - contentHeight) * aspect.AlignY;
            if (!aspect.IsSlice
                && (localX < offsetX
                    || localY < offsetY
                    || localX > offsetX + contentWidth
                    || localY > offsetY + contentHeight))
            {
                return false;
            }

            userX = viewport.MinX + ((localX - offsetX) / scale);
            userY = viewport.MinY + ((localY - offsetY) / scale);
            tolerance = HitTolerancePixels / scale;
        }

        if (!double.IsFinite(userX)
            || !double.IsFinite(userY)
            || !double.IsFinite(tolerance))
        {
            return false;
        }

        mappedPoint = new SvgMappedPreviewPoint(
            new SvgVisualPoint(userX, userY),
            tolerance);
        return true;
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0;
}
