using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgElementCreationService
{
    private const double MaximumCoordinateMagnitude = 1_000_000;
    private const double MinimumLength = 0.001;
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgValidationService _validationService = new();

    public SvgAuthoringAvailability GetAvailability(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? selection,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);

        SvgElementNode? parent = ResolveInsertionParent(document, selection);
        if (parent is null
            || !SvgSourceMutationUtilities.IsCurrentElement(source, parent))
        {
            return new SvgAuthoringAvailability(
                false,
                "Creation is unavailable until the current SVG is valid and indexed.");
        }
        if (SvgLayerPolicy.IsInsideDefinitionContainer(document, parent))
        {
            return new SvgAuthoringAvailability(
                false,
                "Visual artwork cannot be created in a definition container.");
        }
        if (isEffectivelyLocked?.Invoke(parent) == true)
        {
            return new SvgAuthoringAvailability(
                false,
                "Unlock the destination group before creating artwork inside it.");
        }

        return new SvgAuthoringAvailability(true);
    }

    public SvgAuthoringEditResult CreateEdit(
        string source,
        SvgDocumentIndex document,
        SvgElementNode? selection,
        SvgCreateElementKind kind,
        SvgCanvasSize canvasSize,
        Func<SvgElementNode, bool>? isEffectivelyLocked = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        if (!Enum.IsDefined(kind))
        {
            return SvgAuthoringEditResult.Invalid(
                "The requested SVG element type is not supported.");
        }

        SvgAuthoringAvailability availability = GetAvailability(
            source,
            document,
            selection,
            isEffectivelyLocked);
        if (!availability.CanExecute)
        {
            return SvgAuthoringEditResult.Invalid(
                availability.UnavailableReason
                ?? "The element cannot be created.");
        }

        SvgElementNode parent = ResolveInsertionParent(document, selection)!;
        AuthoringCanvas canvas = ReadCanvas(document, canvasSize);
        string id = CreateUniqueId(GetIdStem(kind), document);
        string fragment = CreateFragment(kind, id, canvas);
        if (!SvgSourceMutationUtilities.TryInsertFrontmostChild(
                source,
                parent,
                fragment,
                out string candidate,
                out int insertedStart,
                out string? insertionError))
        {
            return SvgAuthoringEditResult.Invalid(
                insertionError ?? "The element could not be inserted.");
        }

        SvgValidationResult validation = _validationService.Validate(candidate);
        SvgDocumentIndexResult rebuilt = _indexService.Build(candidate);
        if (!validation.IsValid || rebuilt.Document is null)
        {
            return SvgAuthoringEditResult.Invalid(
                $"The new element would make the SVG invalid: {validation.Message}");
        }

        SvgElementNode? created = rebuilt.Document.FindElementAtOffset(
            Math.Min(insertedStart + 1, candidate.Length - 1));
        if (created is null
            || created.Id?.Equals(id, StringComparison.Ordinal) != true)
        {
            return SvgAuthoringEditResult.Invalid(
                "The new element could not be identified safely after insertion.");
        }

        return SvgAuthoringEditResult.Success(
            SvgSourceMutationUtilities.CreateMinimalEdit(source, candidate),
            created.Identity);
    }

    internal static SvgElementNode? ResolveInsertionParent(
        SvgDocumentIndex document,
        SvgElementNode? selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (selection is not null
            && SvgLayerPolicy.IsLayerElement(selection.Name)
            && !SvgLayerPolicy.IsInsideDefinitionContainer(document, selection))
        {
            if (SvgLayerPolicy.IsGroup(selection.Name))
            {
                return selection;
            }

            SvgElementNode? parent = document.FindParent(selection);
            if (parent is not null
                && parent.Name is "svg" or "g"
                && !SvgLayerPolicy.IsInsideDefinitionContainer(document, parent))
            {
                return parent;
            }
        }

        return SvgSourceMutationUtilities.FindSvgRoot(document);
    }

    private static AuthoringCanvas ReadCanvas(
        SvgDocumentIndex document,
        SvgCanvasSize fallback)
    {
        SvgElementNode? root = SvgSourceMutationUtilities.FindSvgRoot(document);
        if (root?.FindAttribute("viewBox") is SvgAttributeSpan viewBox
            && SvgXmlAttributeValueDecoder.TryDecode(
                viewBox.RawValue,
                out string decoded))
        {
            string[] parts = decoded.Split(
                [' ', '\t', '\r', '\n', ','],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
            if (parts.Length == 4
                && parts.All(part => double.TryParse(
                    part,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _)))
            {
                double minX = double.Parse(parts[0], CultureInfo.InvariantCulture);
                double minY = double.Parse(parts[1], CultureInfo.InvariantCulture);
                double width = double.Parse(parts[2], CultureInfo.InvariantCulture);
                double height = double.Parse(parts[3], CultureInfo.InvariantCulture);
                if (IsSafeCanvas(minX, minY, width, height))
                {
                    return new AuthoringCanvas(minX, minY, width, height);
                }
            }
        }

        double fallbackWidth = IsSafeLength(fallback.Width)
            ? fallback.Width
            : 300;
        double fallbackHeight = IsSafeLength(fallback.Height)
            ? fallback.Height
            : 150;
        return new AuthoringCanvas(0, 0, fallbackWidth, fallbackHeight);
    }

    private static bool IsSafeCanvas(
        double minX,
        double minY,
        double width,
        double height) =>
        double.IsFinite(minX)
        && double.IsFinite(minY)
        && IsSafeLength(width)
        && IsSafeLength(height)
        && Math.Abs(minX) <= MaximumCoordinateMagnitude
        && Math.Abs(minY) <= MaximumCoordinateMagnitude
        && Math.Abs(minX + width) <= MaximumCoordinateMagnitude
        && Math.Abs(minY + height) <= MaximumCoordinateMagnitude;

    private static bool IsSafeLength(double value) =>
        double.IsFinite(value)
        && value >= MinimumLength
        && value <= MaximumCoordinateMagnitude;

    private static string CreateFragment(
        SvgCreateElementKind kind,
        string id,
        AuthoringCanvas canvas)
    {
        double centerX = canvas.MinX + canvas.Width / 2;
        double centerY = canvas.MinY + canvas.Height / 2;
        double width = BoundLength(canvas.Width * 0.25);
        double height = BoundLength(canvas.Height * 0.25);
        double radius = BoundLength(Math.Min(canvas.Width, canvas.Height) * 0.12);
        double radiusX = BoundLength(canvas.Width * 0.125);
        double radiusY = BoundLength(canvas.Height * 0.125);
        double strokeWidth = BoundLength(
            Math.Min(canvas.Width, canvas.Height) * 0.01,
            maximum: 20);
        double fontSize = BoundLength(
            Math.Min(canvas.Width, canvas.Height) * 0.12,
            maximum: 96);

        return kind switch
        {
            SvgCreateElementKind.Rectangle =>
                $"<rect id=\"{id}\" x=\"{Format(centerX - width / 2)}\" y=\"{Format(centerY - height / 2)}\" width=\"{Format(width)}\" height=\"{Format(height)}\" fill=\"#2563eb\" />",
            SvgCreateElementKind.Circle =>
                $"<circle id=\"{id}\" cx=\"{Format(centerX)}\" cy=\"{Format(centerY)}\" r=\"{Format(radius)}\" fill=\"#2563eb\" />",
            SvgCreateElementKind.Ellipse =>
                $"<ellipse id=\"{id}\" cx=\"{Format(centerX)}\" cy=\"{Format(centerY)}\" rx=\"{Format(radiusX)}\" ry=\"{Format(radiusY)}\" fill=\"#2563eb\" />",
            SvgCreateElementKind.Line =>
                $"<line id=\"{id}\" x1=\"{Format(centerX - width / 2)}\" y1=\"{Format(centerY)}\" x2=\"{Format(centerX + width / 2)}\" y2=\"{Format(centerY)}\" stroke=\"#2563eb\" stroke-width=\"{Format(strokeWidth)}\" />",
            SvgCreateElementKind.Text =>
                $"<text id=\"{id}\" x=\"{Format(centerX)}\" y=\"{Format(centerY)}\" fill=\"#0f172a\" font-family=\"Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" text-anchor=\"middle\">Text</text>",
            SvgCreateElementKind.Group => $"<g id=\"{id}\"></g>",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static double BoundLength(
        double value,
        double maximum = MaximumCoordinateMagnitude) =>
        Math.Clamp(value, MinimumLength, maximum);

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetIdStem(SvgCreateElementKind kind) =>
        kind switch
        {
            SvgCreateElementKind.Rectangle => "rect",
            SvgCreateElementKind.Circle => "circle",
            SvgCreateElementKind.Ellipse => "ellipse",
            SvgCreateElementKind.Line => "line",
            SvgCreateElementKind.Text => "text",
            SvgCreateElementKind.Group => "group",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    internal static string CreateUniqueId(
        string stem,
        SvgDocumentIndex document,
        ISet<string>? additionalReserved = null)
    {
        HashSet<string> used = document.Elements
            .Select(element => element.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        if (additionalReserved is not null)
        {
            used.UnionWith(additionalReserved);
        }

        if (!used.Contains(stem))
        {
            return stem;
        }
        for (int suffix = 2; suffix <= document.Elements.Count + 2; suffix++)
        {
            string candidate = $"{stem}-{suffix}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("A bounded unique SVG ID could not be generated.");
    }

    private readonly record struct AuthoringCanvas(
        double MinX,
        double MinY,
        double Width,
        double Height);
}
