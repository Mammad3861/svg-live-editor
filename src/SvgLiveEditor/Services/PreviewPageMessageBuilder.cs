using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPageMessageBuilder
{
    public const double MaximumRenderedDimension = 10_000_000;
    public const double MaximumDragThreshold = 1_000;
    public const double MaximumHorizontalScrollDelta =
        PreviewNativeHorizontalScrollPolicy.MaximumDeltaPixels;

    public string BuildZoomStateMessage(
        string bridgeToken,
        double renderedWidth,
        double renderedHeight,
        PreviewViewportPosition viewport)
    {
        ValidateBridgeToken(bridgeToken);
        ValidateDimension(renderedWidth, nameof(renderedWidth));
        ValidateDimension(renderedHeight, nameof(renderedHeight));
        ValidateNormalized(viewport.CenterX, nameof(viewport));
        ValidateNormalized(viewport.CenterY, nameof(viewport));

        return JsonSerializer.Serialize(new
        {
            type = "zoomState",
            token = bridgeToken,
            renderedWidth,
            renderedHeight,
            centerX = viewport.CenterX,
            centerY = viewport.CenterY
        });
    }

    public string BuildPanStateMessage(
        string bridgeToken,
        bool enabled,
        double minimumHorizontalDragDistance,
        double minimumVerticalDragDistance)
    {
        ValidateHexToken(bridgeToken, nameof(bridgeToken));
        ValidateDragThreshold(
            minimumHorizontalDragDistance,
            nameof(minimumHorizontalDragDistance));
        ValidateDragThreshold(
            minimumVerticalDragDistance,
            nameof(minimumVerticalDragDistance));
        return JsonSerializer.Serialize(new
        {
            type = "panState",
            token = bridgeToken,
            enabled,
            minimumHorizontalDragDistance,
            minimumVerticalDragDistance
        });
    }

    public string BuildPngRequestMessage(
        string bridgeToken,
        string requestId,
        PreviewPngSize size)
    {
        ValidateHexToken(bridgeToken, nameof(bridgeToken));
        ValidateHexToken(requestId, nameof(requestId));
        if (size.Width <= 0
            || size.Width > PreviewPngSizeCalculator.MaximumDimension
            || size.Height <= 0
            || size.Height > PreviewPngSizeCalculator.MaximumDimension
            || size.PixelCount > PreviewPngSizeCalculator.MaximumPixelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        return JsonSerializer.Serialize(new
        {
            type = "copyPng",
            token = bridgeToken,
            requestId,
            width = size.Width,
            height = size.Height
        });
    }

    public string BuildHorizontalScrollMessage(
        string bridgeToken,
        double deltaX)
    {
        ValidateBridgeToken(bridgeToken);
        if (!double.IsFinite(deltaX)
            || deltaX == 0
            || Math.Abs(deltaX) > MaximumHorizontalScrollDelta)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        }

        return JsonSerializer.Serialize(new
        {
            type = "horizontalScroll",
            token = bridgeToken,
            deltaX
        });
    }

    public string BuildVisualSelectionMessage(
        string bridgeToken,
        long sourceRevision,
        PreviewVisualSelection? selection)
    {
        ValidateBridgeToken(bridgeToken);
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }

        PreviewVisualSelection value = selection ?? new PreviewVisualSelection(
            SvgVisualElementKind.Rect,
            new SvgVisualShapeGeometry(
                SvgVisualElementKind.Rect,
                0,
                0,
                0,
                0),
            0,
            0);
        ValidateVisualCoordinate(value.Geometry.X1, nameof(selection));
        ValidateVisualCoordinate(value.Geometry.Y1, nameof(selection));
        ValidateVisualCoordinate(value.Geometry.X2, nameof(selection));
        ValidateVisualCoordinate(value.Geometry.Y2, nameof(selection));
        ValidateVisualCoordinate(value.DeltaX, nameof(selection));
        ValidateVisualCoordinate(value.DeltaY, nameof(selection));

        return JsonSerializer.Serialize(new
        {
            type = "visualSelection",
            token = bridgeToken,
            sourceRevision,
            visible = selection is not null,
            kind = selection is null
                ? "none"
                : value.Kind switch
                {
                    SvgVisualElementKind.Rect
                        or SvgVisualElementKind.Unsupported => "rect",
                    SvgVisualElementKind.Circle
                        or SvgVisualElementKind.Ellipse => "ellipse",
                    SvgVisualElementKind.Line => "line",
                    SvgVisualElementKind.Text => "text",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(selection))
                },
            x1 = value.Geometry.X1,
            y1 = value.Geometry.Y1,
            x2 = value.Geometry.X2,
            y2 = value.Geometry.Y2,
            deltaX = value.DeltaX,
            deltaY = value.DeltaY
        });
    }

    public string BuildTextMeasurementMessage(
        string bridgeToken,
        long sourceRevision,
        string requestId,
        IReadOnlyList<SvgVisualTextMeasurementSpec> items)
    {
        ValidateBridgeToken(bridgeToken);
        ValidateHexToken(requestId, nameof(requestId));
        ArgumentNullException.ThrowIfNull(items);
        if (sourceRevision < 0
            || items.Count == 0
            || items.Count > SvgVisualTextIndexService.MaximumMeasuredTextElements
            || items.Select(item => item.Index).Distinct().Count()
                != items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(items));
        }

        foreach (SvgVisualTextMeasurementSpec item in items)
        {
            if (item.Index < 0
                || item.Index >= SvgVisualTextIndexService.MaximumMeasuredTextElements
                || item.Text.Length == 0
                || item.Text.Length > SvgVisualTextIndexService.MaximumTextLength
                || item.Text.Any(char.IsControl)
                || SvgFontFamilyValueValidator.Validate(item.FontFamily)
                    is not null
                || item.FontSize <= 0
                || !double.IsFinite(item.FontSize)
                || item.FontSize
                    > SvgVisualLengthParser.MaximumAbsoluteValue
                || item.FontWeight is not (
                    "normal" or "bold" or "100" or "200" or "300"
                    or "400" or "500" or "600" or "700" or "800"
                    or "900")
                || item.FontStyle is not (
                    "normal" or "italic" or "oblique")
                || item.TextAnchor is not (
                    "start" or "middle" or "end")
                || item.Direction is not ("ltr" or "rtl")
                || item.UnicodeBidi is not (
                    "normal" or "embed" or "isolate" or "plaintext"))
            {
                throw new ArgumentOutOfRangeException(nameof(items));
            }
            ValidateVisualCoordinate(item.X, nameof(items));
            ValidateVisualCoordinate(item.Y, nameof(items));
        }

        return JsonSerializer.Serialize(new
        {
            type = "measureText",
            token = bridgeToken,
            sourceRevision,
            requestId,
            items = items.Select(item => new
            {
                index = item.Index,
                text = item.Text,
                x = item.X,
                y = item.Y,
                fontSize = item.FontSize,
                fontFamily = item.FontFamily,
                fontWeight = item.FontWeight,
                fontStyle = item.FontStyle,
                textAnchor = item.TextAnchor,
                direction = item.Direction,
                unicodeBidi = item.UnicodeBidi
            })
        });
    }

    private static void ValidateBridgeToken(string bridgeToken) =>
        ValidateHexToken(bridgeToken, nameof(bridgeToken));

    private static void ValidateHexToken(
        string value,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "The value must contain exactly 32 hexadecimal characters.",
                parameterName);
        }
    }

    private static void ValidateDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value)
            || value <= 0
            || value > MaximumRenderedDimension)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNormalized(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateDragThreshold(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value)
            || value <= 0
            || value > MaximumDragThreshold)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateVisualCoordinate(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value)
            || Math.Abs(value)
                > SvgVisualLengthParser.MaximumAbsoluteValue)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
