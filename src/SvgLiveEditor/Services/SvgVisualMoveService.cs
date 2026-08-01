using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualMoveService
{
    private const double MaximumDelta = 1_000_000;
    private readonly SvgValidationService _validationService = new();

    public SvgAttributeEditResult CreateEdit(
        string source,
        SvgVisualElement element,
        double deltaX,
        double deltaY)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(element);
        if (!element.IsMovable)
        {
            return SvgAttributeEditResult.Invalid(
                element.UnsupportedReason
                ?? "The selected element cannot be moved visually.");
        }
        if (!double.IsFinite(deltaX)
            || !double.IsFinite(deltaY)
            || Math.Abs(deltaX) > MaximumDelta
            || Math.Abs(deltaY) > MaximumDelta)
        {
            return SvgAttributeEditResult.Invalid(
                "The requested movement is outside the supported range.");
        }

        SvgElementNode sourceElement = element.SourceElement;
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

        (string Name, double Delta)[] movements =
            GetMovements(element.Kind, deltaX, deltaY);
        List<(int Start, int Length, string Value)> replacements = [];
        List<string> insertions = [];
        foreach ((string name, double delta) in movements)
        {
            if (Math.Abs(delta) < 0.0000005)
            {
                continue;
            }

            SvgAttributeSpan? attribute = sourceElement.FindAttribute(name);
            if (!SvgVisualLengthParser.TryParse(
                    attribute?.RawValue,
                    0,
                    out double original,
                    out string suffix))
            {
                return SvgAttributeEditResult.Invalid(
                    $"Visual editing requires unitless or px {name} geometry.");
            }

            string value = FormatMovedValue(
                attribute?.RawValue,
                original + delta,
                suffix);
            if (attribute is null)
            {
                insertions.Add($"{name}=\"{value}\"");
                continue;
            }

            int relativeStart = attribute.ValueSpan.Start - span.Start;
            if (relativeStart < 0
                || attribute.ValueSpan.Length < 0
                || relativeStart
                    > startTag.Length - attribute.ValueSpan.Length
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
                 replacements.OrderByDescending(replacement =>
                     replacement.Start))
        {
            updatedTag = string.Concat(
                updatedTag.AsSpan(0, start),
                value,
                updatedTag.AsSpan(start + length));
        }

        if (insertions.Count > 0)
        {
            int closingOffset = updatedTag.Length - 1;
            if (closingOffset > 0
                && updatedTag[closingOffset - 1] == '/')
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
                $"The movement would make the SVG invalid: {validation.Message}");
    }

    private static (string Name, double Delta)[] GetMovements(
        SvgVisualElementKind kind,
        double deltaX,
        double deltaY) => kind switch
    {
        SvgVisualElementKind.Rect
            or SvgVisualElementKind.Text =>
            [("x", deltaX), ("y", deltaY)],
        SvgVisualElementKind.Circle
            or SvgVisualElementKind.Ellipse =>
            [("cx", deltaX), ("cy", deltaY)],
        SvgVisualElementKind.Line =>
            [
                ("x1", deltaX),
                ("y1", deltaY),
                ("x2", deltaX),
                ("y2", deltaY)
            ],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string FormatMovedValue(
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
        if (Math.Abs(rounded - Math.Round(rounded)) < 0.0000005)
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
