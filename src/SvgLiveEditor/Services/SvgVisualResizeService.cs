using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualResizeService
{
    public const double MinimumDimension = 0.01;

    private const double EqualityTolerance = 0.0000005;
    private readonly SvgValidationService _validationService = new();
    private readonly SvgVisualResizeHandleService _handleService = new();

    public bool TryCalculate(
        SvgVisualElement element,
        SvgResizeHandle handle,
        SvgVisualPoint pointer,
        bool preserveAspectRatio,
        out SvgVisualShapeGeometry geometry,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(element);
        geometry = element.Geometry ?? new SvgVisualShapeGeometry(
            element.Kind,
            0,
            0,
            0,
            0);
        error = null;
        if (!element.IsResizable
            || element.Geometry is not SvgVisualShapeGeometry original
            || !_handleService.IsAllowed(element, handle))
        {
            error = "The selected element does not support this resize handle.";
            return false;
        }
        if (!IsBounded(pointer.X) || !IsBounded(pointer.Y))
        {
            error = "The requested resize is outside the supported range.";
            return false;
        }

        bool calculated = element.Kind switch
        {
            SvgVisualElementKind.Rect or SvgVisualElementKind.Ellipse =>
                TryResizeBox(
                    original,
                    handle,
                    pointer,
                    preserveAspectRatio
                        && SvgVisualResizeHandleService.IsCorner(handle),
                    out geometry),
            SvgVisualElementKind.Circle => TryResizeCircle(
                original,
                handle,
                pointer,
                out geometry),
            SvgVisualElementKind.Line => TryResizeLine(
                original,
                handle,
                pointer,
                out geometry),
            _ => false
        };
        if (!calculated || !IsValidGeometry(element.Kind, geometry))
        {
            geometry = original;
            error = "The requested resize is outside the supported range.";
            return false;
        }

        return true;
    }

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgVisualElement element,
        SvgVisualShapeGeometry resizedGeometry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(resizedGeometry);
        if (!element.IsResizable
            || element.Geometry is not SvgVisualShapeGeometry original
            || resizedGeometry.Kind != element.Kind
            || !IsValidGeometry(element.Kind, resizedGeometry))
        {
            return SvgAttributeEditResult.Invalid(
                "The selected element cannot be resized visually.");
        }

        List<(string Name, double Original, double Updated)> updates =
            BuildUpdates(element.Kind, original, resizedGeometry);
        return CreateSourceEdit(source, element.SourceElement, updates);
    }

    private SvgAttributeEditResult CreateSourceEdit(
        string source,
        SvgElementNode sourceElement,
        IReadOnlyList<(string Name, double Original, double Updated)> updates)
    {
        SourceSpan span = sourceElement.StartTagSpan;
        if (span.Start < 0
            || span.Length <= 0
            || span.Start > source.Length - span.Length)
        {
            return Stale();
        }

        string startTag = source.Substring(span.Start, span.Length);
        if (!startTag.StartsWith(
                $"<{sourceElement.QualifiedName}",
                StringComparison.Ordinal)
            || !startTag.EndsWith('>'))
        {
            return Stale();
        }

        List<(int Start, int Length, string Value)> replacements = [];
        List<string> insertions = [];
        foreach ((string name, double original, double updated) in updates)
        {
            if (Math.Abs(updated - original) < EqualityTolerance)
            {
                continue;
            }

            SvgAttributeSpan? attribute = sourceElement.FindAttribute(name);
            if (!SvgVisualLengthParser.TryParse(
                    attribute?.RawValue,
                    0,
                    out _,
                    out string suffix))
            {
                return SvgAttributeEditResult.Invalid(
                    $"Visual resizing requires unitless or px {name} geometry.");
            }

            string value = FormatValue(attribute?.RawValue, updated, suffix);
            if (attribute is null)
            {
                insertions.Add($"{name}=\"{value}\"");
                continue;
            }

            int relativeStart = attribute.ValueSpan.Start - span.Start;
            if (relativeStart < 0
                || attribute.ValueSpan.Length < 0
                || relativeStart > startTag.Length - attribute.ValueSpan.Length
                || !startTag.AsSpan(
                        relativeStart,
                        attribute.ValueSpan.Length)
                    .SequenceEqual(attribute.RawValue))
            {
                return Stale();
            }

            replacements.Add((
                relativeStart,
                attribute.ValueSpan.Length,
                value));
        }

        string updatedTag = startTag;
        foreach ((int start, int length, string value) in
                 replacements.OrderByDescending(item => item.Start))
        {
            updatedTag = string.Concat(
                updatedTag.AsSpan(0, start),
                value,
                updatedTag.AsSpan(start + length));
        }

        if (insertions.Count > 0)
        {
            int closingOffset = updatedTag.Length - 1;
            if (closingOffset > 0 && updatedTag[closingOffset - 1] == '/')
            {
                closingOffset--;
            }
            string leadingSpace = closingOffset > 0
                && char.IsWhiteSpace(updatedTag[closingOffset - 1])
                    ? string.Empty
                    : " ";
            updatedTag = updatedTag.Insert(
                closingOffset,
                $"{leadingSpace}{string.Join(" ", insertions)}");
        }

        if (updatedTag.Equals(startTag, StringComparison.Ordinal))
        {
            return SvgAttributeEditResult.Success(edit: null);
        }

        SourceTextEdit edit = new(span.Start, span.Length, updatedTag);
        SvgValidationResult validation =
            _validationService.Validate(edit.Apply(source));
        return validation.IsValid
            ? SvgAttributeEditResult.Success(edit)
            : SvgAttributeEditResult.Invalid(
                $"The resize would make the SVG invalid: {validation.Message}");
    }

    private static bool TryResizeBox(
        SvgVisualShapeGeometry original,
        SvgResizeHandle handle,
        SvgVisualPoint pointer,
        bool preserveAspectRatio,
        out SvgVisualShapeGeometry resized)
    {
        SvgVisualBounds bounds = original.Bounds;
        bool movesLeft = handle is SvgResizeHandle.TopLeft
            or SvgResizeHandle.Left
            or SvgResizeHandle.BottomLeft;
        bool movesRight = handle is SvgResizeHandle.TopRight
            or SvgResizeHandle.Right
            or SvgResizeHandle.BottomRight;
        bool movesTop = handle is SvgResizeHandle.TopLeft
            or SvgResizeHandle.Top
            or SvgResizeHandle.TopRight;
        bool movesBottom = handle is SvgResizeHandle.BottomLeft
            or SvgResizeHandle.Bottom
            or SvgResizeHandle.BottomRight;
        if ((!movesLeft && !movesRight) || (!movesTop && !movesBottom))
        {
            double left = movesLeft
                ? Math.Min(pointer.X, bounds.Right - MinimumDimension)
                : bounds.Left;
            double right = movesRight
                ? Math.Max(pointer.X, bounds.Left + MinimumDimension)
                : bounds.Right;
            double top = movesTop
                ? Math.Min(pointer.Y, bounds.Bottom - MinimumDimension)
                : bounds.Top;
            double bottom = movesBottom
                ? Math.Max(pointer.Y, bounds.Top + MinimumDimension)
                : bounds.Bottom;
            resized = new SvgVisualShapeGeometry(
                original.Kind,
                left,
                top,
                right,
                bottom);
            return true;
        }

        double anchorX = movesLeft ? bounds.Right : bounds.Left;
        double anchorY = movesTop ? bounds.Bottom : bounds.Top;
        double requestedWidth = movesLeft
            ? anchorX - pointer.X
            : pointer.X - anchorX;
        double requestedHeight = movesTop
            ? anchorY - pointer.Y
            : pointer.Y - anchorY;
        requestedWidth = Math.Max(MinimumDimension, requestedWidth);
        requestedHeight = Math.Max(MinimumDimension, requestedHeight);
        if (preserveAspectRatio)
        {
            double scale = Math.Max(
                requestedWidth / bounds.Width,
                requestedHeight / bounds.Height);
            requestedWidth = Math.Max(MinimumDimension, bounds.Width * scale);
            requestedHeight = Math.Max(MinimumDimension, bounds.Height * scale);
        }

        double cornerLeft = movesLeft ? anchorX - requestedWidth : anchorX;
        double cornerRight = movesLeft ? anchorX : anchorX + requestedWidth;
        double cornerTop = movesTop ? anchorY - requestedHeight : anchorY;
        double cornerBottom = movesTop ? anchorY : anchorY + requestedHeight;
        resized = new SvgVisualShapeGeometry(
            original.Kind,
            cornerLeft,
            cornerTop,
            cornerRight,
            cornerBottom);
        return true;
    }

    private static bool TryResizeCircle(
        SvgVisualShapeGeometry original,
        SvgResizeHandle handle,
        SvgVisualPoint pointer,
        out SvgVisualShapeGeometry resized)
    {
        SvgVisualBounds bounds = original.Bounds;
        double centerX = (bounds.Left + bounds.Right) / 2;
        double centerY = (bounds.Top + bounds.Bottom) / 2;
        double diameter;
        switch (handle)
        {
            case SvgResizeHandle.Left:
                diameter = Math.Max(
                    MinimumDimension,
                    bounds.Right - pointer.X);
                resized = CircleFromCenter(
                    bounds.Right - (diameter / 2),
                    centerY,
                    diameter);
                return true;
            case SvgResizeHandle.Right:
                diameter = Math.Max(
                    MinimumDimension,
                    pointer.X - bounds.Left);
                resized = CircleFromCenter(
                    bounds.Left + (diameter / 2),
                    centerY,
                    diameter);
                return true;
            case SvgResizeHandle.Top:
                diameter = Math.Max(
                    MinimumDimension,
                    bounds.Bottom - pointer.Y);
                resized = CircleFromCenter(
                    centerX,
                    bounds.Bottom - (diameter / 2),
                    diameter);
                return true;
            case SvgResizeHandle.Bottom:
                diameter = Math.Max(
                    MinimumDimension,
                    pointer.Y - bounds.Top);
                resized = CircleFromCenter(
                    centerX,
                    bounds.Top + (diameter / 2),
                    diameter);
                return true;
            default:
                resized = original;
                return false;
        }
    }

    private static SvgVisualShapeGeometry CircleFromCenter(
        double centerX,
        double centerY,
        double diameter)
    {
        double radius = diameter / 2;
        return new SvgVisualShapeGeometry(
            SvgVisualElementKind.Circle,
            centerX - radius,
            centerY - radius,
            centerX + radius,
            centerY + radius);
    }

    private static bool TryResizeLine(
        SvgVisualShapeGeometry original,
        SvgResizeHandle handle,
        SvgVisualPoint pointer,
        out SvgVisualShapeGeometry resized)
    {
        resized = handle switch
        {
            SvgResizeHandle.Start => new SvgVisualShapeGeometry(
                SvgVisualElementKind.Line,
                pointer.X,
                pointer.Y,
                original.X2,
                original.Y2),
            SvgResizeHandle.End => new SvgVisualShapeGeometry(
                SvgVisualElementKind.Line,
                original.X1,
                original.Y1,
                pointer.X,
                pointer.Y),
            _ => original
        };
        return handle is SvgResizeHandle.Start or SvgResizeHandle.End;
    }

    private static List<(string Name, double Original, double Updated)>
        BuildUpdates(
            SvgVisualElementKind kind,
            SvgVisualShapeGeometry original,
            SvgVisualShapeGeometry resized)
    {
        SvgVisualBounds oldBounds = original.Bounds;
        SvgVisualBounds newBounds = resized.Bounds;
        return kind switch
        {
            SvgVisualElementKind.Rect =>
            [
                ("x", oldBounds.Left, newBounds.Left),
                ("y", oldBounds.Top, newBounds.Top),
                ("width", oldBounds.Width, newBounds.Width),
                ("height", oldBounds.Height, newBounds.Height)
            ],
            SvgVisualElementKind.Ellipse =>
            [
                ("cx", Midpoint(oldBounds.Left, oldBounds.Right),
                    Midpoint(newBounds.Left, newBounds.Right)),
                ("cy", Midpoint(oldBounds.Top, oldBounds.Bottom),
                    Midpoint(newBounds.Top, newBounds.Bottom)),
                ("rx", oldBounds.Width / 2, newBounds.Width / 2),
                ("ry", oldBounds.Height / 2, newBounds.Height / 2)
            ],
            SvgVisualElementKind.Circle =>
            [
                ("cx", Midpoint(oldBounds.Left, oldBounds.Right),
                    Midpoint(newBounds.Left, newBounds.Right)),
                ("cy", Midpoint(oldBounds.Top, oldBounds.Bottom),
                    Midpoint(newBounds.Top, newBounds.Bottom)),
                ("r", oldBounds.Width / 2, newBounds.Width / 2)
            ],
            SvgVisualElementKind.Line =>
            [
                ("x1", original.X1, resized.X1),
                ("y1", original.Y1, resized.Y1),
                ("x2", original.X2, resized.X2),
                ("y2", original.Y2, resized.Y2)
            ],
            _ => []
        };
    }

    private static bool IsValidGeometry(
        SvgVisualElementKind kind,
        SvgVisualShapeGeometry geometry)
    {
        if (geometry.Kind != kind
            || !IsBounded(geometry.X1)
            || !IsBounded(geometry.Y1)
            || !IsBounded(geometry.X2)
            || !IsBounded(geometry.Y2))
        {
            return false;
        }

        SvgVisualBounds bounds = geometry.Bounds;
        if (!IsBounded(bounds.Width) || !IsBounded(bounds.Height))
        {
            return false;
        }
        if (kind is SvgVisualElementKind.Rect
            or SvgVisualElementKind.Ellipse
            or SvgVisualElementKind.Circle)
        {
            if (bounds.Width < MinimumDimension
                || bounds.Height < MinimumDimension)
            {
                return false;
            }
        }
        return kind != SvgVisualElementKind.Circle
            || Math.Abs(bounds.Width - bounds.Height) < EqualityTolerance;
    }

    private static bool IsBounded(double value) =>
        double.IsFinite(value)
        && Math.Abs(value) <= SvgVisualLengthParser.MaximumAbsoluteValue;

    private static double Midpoint(double first, double second) =>
        first + ((second - first) / 2);

    private static string FormatValue(
        string? originalRawValue,
        double value,
        string suffix)
    {
        string original = originalRawValue?.Trim() ?? string.Empty;
        if (suffix.Length > 0)
        {
            original = original[..^suffix.Length].TrimEnd();
        }

        int decimalPlaces = GetDecimalPlaces(original);
        double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        string formatted;
        if (Math.Abs(rounded - Math.Round(rounded)) < EqualityTolerance)
        {
            formatted = decimalPlaces > 0
                ? rounded.ToString(
                    $"F{Math.Min(decimalPlaces, 6)}",
                    CultureInfo.InvariantCulture)
                : Math.Round(rounded).ToString(
                    "0",
                    CultureInfo.InvariantCulture);
        }
        else
        {
            int precision = Math.Max(decimalPlaces, 6);
            formatted = rounded.ToString(
                $"F{Math.Min(precision, 6)}",
                CultureInfo.InvariantCulture);
            int minimumLength = decimalPlaces > 0
                ? formatted.IndexOf('.') + 1 + Math.Min(decimalPlaces, 6)
                : 0;
            while (formatted.EndsWith('0')
                && formatted.Length > minimumLength)
            {
                formatted = formatted[..^1];
            }
            if (formatted.EndsWith('.'))
            {
                formatted = formatted[..^1];
            }
        }

        return formatted == "-0" ? $"0{suffix}" : $"{formatted}{suffix}";
    }

    private static int GetDecimalPlaces(string number)
    {
        int exponent = number.IndexOfAny(['e', 'E']);
        string mantissa = exponent >= 0 ? number[..exponent] : number;
        int separator = mantissa.IndexOf('.');
        return separator < 0 ? 0 : mantissa.Length - separator - 1;
    }

    private static SvgAttributeEditResult Stale() =>
        SvgAttributeEditResult.Invalid(
            "The source changed; select the element again.");
}
