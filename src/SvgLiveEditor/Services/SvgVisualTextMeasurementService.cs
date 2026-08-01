using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualTextMeasurementService
{
    public SvgVisualDocument Apply(
        SvgVisualDocument document,
        IReadOnlyList<SvgVisualTextMeasurementResult> results)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(results);
        Dictionary<int, SvgVisualTextMeasurementResult> byIndex =
            results.ToDictionary(result => result.Index);
        SvgVisualElement[] elements = document.Elements
            .Select(element => Apply(element, byIndex))
            .ToArray();
        return new SvgVisualDocument(document.Viewport, elements);
    }

    private static SvgVisualElement Apply(
        SvgVisualElement element,
        IReadOnlyDictionary<int, SvgVisualTextMeasurementResult> results)
    {
        if (element.TextMeasurement
                is not SvgVisualTextMeasurementSpec request
            || !results.TryGetValue(
                request.Index,
                out SvgVisualTextMeasurementResult? result))
        {
            return element;
        }

        if (!result.IsSuccess
            || result.Bounds is not SvgVisualBounds bounds
            || !IsValidBounds(bounds))
        {
            return element with
            {
                Geometry = null,
                UnsupportedReason =
                    "WebView2 could not produce reliable bounds for this text."
            };
        }

        return element with
        {
            Geometry = new SvgVisualShapeGeometry(
                SvgVisualElementKind.Text,
                bounds.Left,
                bounds.Top,
                bounds.Right,
                bounds.Bottom),
            UnsupportedReason = null
        };
    }

    private static bool IsValidBounds(SvgVisualBounds bounds) =>
        double.IsFinite(bounds.Left)
        && double.IsFinite(bounds.Top)
        && double.IsFinite(bounds.Right)
        && double.IsFinite(bounds.Bottom)
        && bounds.Width > 0
        && bounds.Height > 0
        && Math.Abs(bounds.Left)
            <= SvgVisualLengthParser.MaximumAbsoluteValue
        && Math.Abs(bounds.Top)
            <= SvgVisualLengthParser.MaximumAbsoluteValue
        && Math.Abs(bounds.Right)
            <= SvgVisualLengthParser.MaximumAbsoluteValue
        && Math.Abs(bounds.Bottom)
            <= SvgVisualLengthParser.MaximumAbsoluteValue;
}
