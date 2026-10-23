using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgTextResizeService
{
    public const double MinimumFontSize =
        SvgVisualTextIndexService.MinimumResizableFontSize;
    public const double MaximumFontSize =
        SvgVisualTextIndexService.MaximumResizableFontSize;

    private readonly SvgVisualResizeHandleService _handleService = new();
    private readonly SvgAttributeEditService _attributeEditService = new();

    public bool TryCalculate(
        SvgVisualElement element,
        SvgResizeHandle handle,
        SvgVisualPoint pointer,
        out SvgTextResizeCandidate candidate,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(element);
        candidate = default;
        error = null;
        if (element.Kind != SvgVisualElementKind.Text
            || handle != SvgResizeHandle.Uniform
            || !element.IsResizable
            || element.Geometry is not SvgVisualShapeGeometry geometry
            || element.TextMeasurement is not SvgVisualTextMeasurementSpec text
            || element.TextResizeSource is not SvgTextResizeSource resizeSource)
        {
            error = element.ResizeUnavailableReason
                ?? "Only eligible measured direct text has a uniform size handle.";
            return false;
        }
        if (!double.IsFinite(pointer.X) || !double.IsFinite(pointer.Y))
        {
            error = "The requested text size is outside the supported range.";
            return false;
        }

        SvgVisualPoint originalHandle = _handleService.Create(element, geometry)
            .Single(item => item.Handle == SvgResizeHandle.Uniform)
            .Point;
        double originalDistance = Distance(
            originalHandle,
            new SvgVisualPoint(text.X, text.Y));
        double requestedDistance = Distance(
            pointer,
            new SvgVisualPoint(text.X, text.Y));
        if (!double.IsFinite(originalDistance)
            || originalDistance <= 0
            || !double.IsFinite(requestedDistance))
        {
            error = "The measured text bounds cannot produce a reliable uniform size.";
            return false;
        }

        double fontSize = Math.Clamp(
            resizeSource.FontSize * requestedDistance / originalDistance,
            MinimumFontSize,
            MaximumFontSize);
        double scale = fontSize / resizeSource.FontSize;
        SvgVisualShapeGeometry previewGeometry = new(
            SvgVisualElementKind.Text,
            ScaleCoordinate(geometry.X1, text.X, scale),
            ScaleCoordinate(geometry.Y1, text.Y, scale),
            ScaleCoordinate(geometry.X2, text.X, scale),
            ScaleCoordinate(geometry.Y2, text.Y, scale));
        if (!IsBounded(previewGeometry))
        {
            error = "The resized text bounds are outside the supported range.";
            return false;
        }

        candidate = new SvgTextResizeCandidate(fontSize, previewGeometry);
        return true;
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgDocumentIndex documentIndex,
        SvgVisualElement element,
        double fontSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(documentIndex);
        ArgumentNullException.ThrowIfNull(element);
        if (element.Kind != SvgVisualElementKind.Text
            || !element.IsResizable
            || element.TextMeasurement is null
            || element.TextResizeSource is not SvgTextResizeSource indexedSource)
        {
            return SvgAttributeEditResult.Invalid(
                element.ResizeUnavailableReason
                ?? "The selected text cannot be resized safely.");
        }
        SvgElementNode? current = documentIndex.FindBestMatch(
            element.SourceElement.Identity);
        SvgVisualTextIndexService? textIndex = null;
        SvgTextResizeSource? currentSource = null;
        string? eligibilityError = null;
        if (current is null
            || current.StartTagSpan != element.SourceElement.StartTagSpan
            || current.FullSpan != element.SourceElement.FullSpan
            || !SvgVisualTextIndexService.TryCreate(
                documentIndex,
                source,
                out textIndex)
            || textIndex is null
            || !textIndex.TryCreateResizeSource(
                current,
                out currentSource,
                out eligibilityError)
            || currentSource is null)
        {
            return SvgAttributeEditResult.Invalid(
                eligibilityError
                ?? "The text resize selection is stale or no longer eligible.");
        }
        if (currentSource != indexedSource)
        {
            return SvgAttributeEditResult.Invalid(
                "The text font-size changed; select it again before resizing.");
        }
        if (!double.IsFinite(fontSize)
            || fontSize < MinimumFontSize
            || fontSize > MaximumFontSize)
        {
            return SvgAttributeEditResult.Invalid(
                $"Text size must be from {MinimumFontSize.ToString(CultureInfo.InvariantCulture)} to {MaximumFontSize.ToString(CultureInfo.InvariantCulture)}.");
        }
        if (Math.Abs(fontSize - currentSource.FontSize) <= 0.000_001)
        {
            return SvgAttributeEditResult.Success(edit: null);
        }

        string value = string.Concat(
            fontSize.ToString("0.###", CultureInfo.InvariantCulture),
            currentSource.Suffix);
        return _attributeEditService.CreateEdit(
            source,
            current,
            "font-size",
            value);
    }

    private static double Distance(SvgVisualPoint first, SvgVisualPoint second)
    {
        double x = first.X - second.X;
        double y = first.Y - second.Y;
        return Math.Sqrt((x * x) + (y * y));
    }

    private static double ScaleCoordinate(
        double coordinate,
        double anchor,
        double scale) =>
        anchor + ((coordinate - anchor) * scale);

    private static bool IsBounded(SvgVisualShapeGeometry geometry) =>
        geometry.Bounds.Width > 0
        && geometry.Bounds.Height > 0
        && new[] { geometry.X1, geometry.Y1, geometry.X2, geometry.Y2 }
            .All(value => double.IsFinite(value)
                && Math.Abs(value)
                    <= SvgVisualLengthParser.MaximumAbsoluteValue);
}
