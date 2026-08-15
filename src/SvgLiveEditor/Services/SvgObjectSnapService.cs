using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgObjectSnapService
{
    public const double ThresholdCssPixels = 4;
    // Three snap thresholds allow a small visual gutter while excluding
    // canvas-distant objects that merely share an axis coordinate.
    public const double OrthogonalProximityCssPixels = 12;
    private const double AmbiguityToleranceCssPixels = 0.25;
    private const double MinimumAppliedCorrectionCssPixels = 0.01;

    public SvgSnapResult Snap(
        IReadOnlyList<SvgVisualElement> moving,
        IReadOnlyList<SvgVisualElement> siblingTargets,
        SvgVisualViewport viewport,
        double requestedDeltaX,
        double requestedDeltaY,
        double svgUnitsPerCssPixelX,
        double svgUnitsPerCssPixelY,
        bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(siblingTargets);
        if (!isEnabled
            || moving.Count == 0
            || moving.Count > SvgMultiSelectionService.MaximumSelectionCount
            || moving.Any(item => item.Geometry is null)
            || !IsPositiveFinite(svgUnitsPerCssPixelX)
            || !IsPositiveFinite(svgUnitsPerCssPixelY)
            || !double.IsFinite(requestedDeltaX)
            || !double.IsFinite(requestedDeltaY))
        {
            return new SvgSnapResult(requestedDeltaX, requestedDeltaY, []);
        }

        SvgVisualBounds originalMovingBounds = Union(moving.Select(item =>
            item.Geometry!.Bounds));
        SvgVisualBounds movingBounds = Translate(
            originalMovingBounds,
            requestedDeltaX,
            requestedDeltaY);
        HashSet<SvgElementIdentity> movingIds = moving
            .Select(item => item.SourceElement.Identity)
            .ToHashSet();
        SvgVisualBounds[] targets = siblingTargets
            .Where(item => item.Geometry is not null
                && !movingIds.Contains(item.SourceElement.Identity)
                && !CoversCanvas(item.Geometry.Bounds, viewport))
            .Select(item => item.Geometry!.Bounds)
            .ToArray();
        double[] xTargets = targets
            .Where(target => OrthogonalGap(
                    movingBounds.Top,
                    movingBounds.Bottom,
                    target.Top,
                    target.Bottom)
                <= OrthogonalProximityCssPixels * svgUnitsPerCssPixelY)
            .SelectMany(AxisX)
            .Append(viewport.MinX + (viewport.Width / 2))
            .Distinct()
            .ToArray();
        double[] yTargets = targets
            .Where(target => OrthogonalGap(
                    movingBounds.Left,
                    movingBounds.Right,
                    target.Left,
                    target.Right)
                <= OrthogonalProximityCssPixels * svgUnitsPerCssPixelX)
            .SelectMany(AxisY)
            .Append(viewport.MinY + (viewport.Height / 2))
            .Distinct()
            .ToArray();

        (double Delta, double? Target) x = FindBest(
            AxisX(movingBounds),
            xTargets,
            requestedDeltaX,
            ThresholdCssPixels * svgUnitsPerCssPixelX,
            AmbiguityToleranceCssPixels * svgUnitsPerCssPixelX,
            MinimumAppliedCorrectionCssPixels * svgUnitsPerCssPixelX);
        (double Delta, double? Target) y = FindBest(
            AxisY(movingBounds),
            yTargets,
            requestedDeltaY,
            ThresholdCssPixels * svgUnitsPerCssPixelY,
            AmbiguityToleranceCssPixels * svgUnitsPerCssPixelY,
            MinimumAppliedCorrectionCssPixels * svgUnitsPerCssPixelY);
        List<PreviewAlignmentGuide> guides = [];
        if (x.Target is double vertical)
        {
            guides.Add(new PreviewAlignmentGuide(
                PreviewAlignmentGuideOrientation.Vertical,
                vertical,
                viewport.MinY,
                viewport.MinY + viewport.Height));
        }
        if (y.Target is double horizontal)
        {
            guides.Add(new PreviewAlignmentGuide(
                PreviewAlignmentGuideOrientation.Horizontal,
                horizontal,
                viewport.MinX,
                viewport.MinX + viewport.Width));
        }
        return new SvgSnapResult(x.Delta, y.Delta, guides);
    }

    private static (double Delta, double? Target) FindBest(
        IEnumerable<double> movingPoints,
        IReadOnlyList<double> targets,
        double requestedDelta,
        double threshold,
        double ambiguityTolerance,
        double minimumAppliedCorrection)
    {
        List<(double Distance, double Adjustment, double Target)> candidates = [];
        foreach (double point in movingPoints)
        {
            foreach (double target in targets)
            {
                double adjustment = target - point;
                double distance = Math.Abs(adjustment);
                if (distance <= threshold)
                {
                    candidates.Add((distance, adjustment, target));
                }
            }
        }
        if (candidates.Count == 0)
        {
            return (requestedDelta, null);
        }

        double bestDistance = candidates.Min(candidate => candidate.Distance);
        if (bestDistance <= minimumAppliedCorrection)
        {
            // An already aligned axis needs no correction and therefore no
            // transient guide. This also prevents a worse secondary target
            // from pulling an exact alignment away from its current value.
            return (requestedDelta, null);
        }
        (double Distance, double Adjustment, double Target)[] tied = candidates
            .Where(candidate => Math.Abs(candidate.Distance - bestDistance)
                <= ambiguityTolerance)
            .ToArray();
        if (tied.Select(candidate => candidate.Target)
            .Distinct()
            .Count() > 1)
        {
            return (requestedDelta, null);
        }

        (double Distance, double Adjustment, double Target) best = tied[0];
        return (requestedDelta + best.Adjustment, best.Target);
    }

    private static bool CoversCanvas(
        SvgVisualBounds bounds,
        SvgVisualViewport viewport)
    {
        const double tolerance = 0.000001;
        return bounds.Left <= viewport.MinX + tolerance
            && bounds.Top <= viewport.MinY + tolerance
            && bounds.Right >= viewport.MinX + viewport.Width - tolerance
            && bounds.Bottom >= viewport.MinY + viewport.Height - tolerance;
    }

    private static double[] AxisX(SvgVisualBounds bounds) =>
        [bounds.Left, (bounds.Left + bounds.Right) / 2, bounds.Right];

    private static double[] AxisY(SvgVisualBounds bounds) =>
        [bounds.Top, (bounds.Top + bounds.Bottom) / 2, bounds.Bottom];

    private static SvgVisualBounds Translate(
        SvgVisualBounds bounds,
        double deltaX,
        double deltaY) => new(
            bounds.Left + deltaX,
            bounds.Top + deltaY,
            bounds.Right + deltaX,
            bounds.Bottom + deltaY);

    private static double OrthogonalGap(
        double firstStart,
        double firstEnd,
        double secondStart,
        double secondEnd)
    {
        if (firstEnd >= secondStart && secondEnd >= firstStart)
        {
            return 0;
        }

        return firstEnd < secondStart
            ? secondStart - firstEnd
            : firstStart - secondEnd;
    }

    private static SvgVisualBounds Union(IEnumerable<SvgVisualBounds> bounds)
    {
        SvgVisualBounds[] values = bounds.ToArray();
        return new SvgVisualBounds(
            values.Min(item => item.Left),
            values.Min(item => item.Top),
            values.Max(item => item.Right),
            values.Max(item => item.Bottom));
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0;
}
