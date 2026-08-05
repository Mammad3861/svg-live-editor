using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgVisualGeometryIndexService
{
    private static readonly HashSet<string> SupportedElementNames =
        new(StringComparer.Ordinal)
        {
            "rect",
            "circle",
            "ellipse",
            "line",
            "text"
        };

    private static readonly HashSet<string> SupportedAncestorNames =
        new(StringComparer.Ordinal)
        {
            "svg",
            "g"
        };

    private static readonly HashSet<string> UnsupportedDirectGraphicsNames =
        new(StringComparer.Ordinal)
        {
            "path",
            "polygon",
            "polyline"
        };

    private static readonly HashSet<string> NonRenderedContainerNames =
        new(StringComparer.Ordinal)
        {
            "defs",
            "clipPath",
            "filter",
            "linearGradient",
            "marker",
            "mask",
            "pattern",
            "radialGradient",
            "symbol"
        };

    private static readonly HashSet<string> AnimationElementNames =
        new(StringComparer.Ordinal)
        {
            "animate",
            "animateMotion",
            "animateTransform",
            "set"
        };

    public SvgVisualDocument Build(
        SvgDocumentIndex documentIndex,
        SvgCanvasSize canvasSize)
    {
        return Build(documentIndex, canvasSize, source: null);
    }

    public SvgVisualDocument Build(
        SvgDocumentIndex documentIndex,
        SvgCanvasSize canvasSize,
        string? source)
    {
        ArgumentNullException.ThrowIfNull(documentIndex);
        SvgElementNode root = documentIndex.Roots.FirstOrDefault()
            ?? throw new ArgumentException(
                "The SVG index has no root element.",
                nameof(documentIndex));
        SvgVisualViewport viewport = ReadViewport(
            root,
            canvasSize,
            out string? viewportError);
        Dictionary<string, SvgElementNode> elementsByPath =
            documentIndex.Elements.ToDictionary(
                element => element.StructuralPath,
                StringComparer.Ordinal);
        List<SvgVisualElement> elements = [];
        SvgVisualTextIndexService? textIndex = null;
        if (source is not null)
        {
            SvgVisualTextIndexService.TryCreate(
                documentIndex,
                source,
                out textIndex);
        }
        int textMeasurementIndex = 0;

        foreach (SvgElementNode element in documentIndex.Elements)
        {
            bool isSupportedName = SupportedElementNames.Contains(element.Name);
            bool isUnsupportedDirectGraphic =
                UnsupportedDirectGraphicsNames.Contains(element.Name);
            if ((!isSupportedName && !isUnsupportedDirectGraphic)
                || IsInsideNonRenderedContainer(
                    element,
                    elementsByPath))
            {
                continue;
            }
            if (SvgVisualStylePolicy.IsDefinitelyNotRendered(
                    element,
                    elementsByPath))
            {
                continue;
            }

            if (isUnsupportedDirectGraphic)
            {
                string? blockerPositionError =
                    GetUnsupportedReason(element, elementsByPath);
                SvgVisualShapeGeometry? blockerGeometry =
                    blockerPositionError is null
                        ? TryReadUnsupportedBlockerGeometry(element)
                        : null;
                string blockerReason = blockerPositionError
                    ?? (blockerGeometry is null
                        ? $"{element.Name} cannot be selected because reliable conservative bounds could not be established."
                        : $"Visual editing is not available for {element.Name} elements in this version.");
                elements.Add(new SvgVisualElement(
                    element,
                    SvgVisualElementKind.Unsupported,
                    blockerGeometry,
                    blockerReason,
                    BlocksLowerVisualHits: true));
                continue;
            }

            SvgVisualElementKind kind = ParseKind(element.Name);
            string? positionalError = viewportError
                ?? GetUnsupportedReason(element, elementsByPath);
            string? unsupportedReason = positionalError;
            if (kind == SvgVisualElementKind.Text)
            {
                SvgVisualTextMeasurementSpec? measurement = null;
                if (unsupportedReason is null)
                {
                    if (textIndex is null)
                    {
                        unsupportedReason =
                            "The text source mapping is unavailable.";
                    }
                    else if (!textIndex.TryCreateMeasurement(
                                 element,
                                 textMeasurementIndex,
                                 out measurement,
                                 out string? textError))
                    {
                        unsupportedReason = textError;
                    }
                }
                if (measurement is not null)
                {
                    textMeasurementIndex++;
                    unsupportedReason =
                        "Text bounds are waiting for trusted WebView2 measurement.";
                }
                elements.Add(new SvgVisualElement(
                    element,
                    kind,
                    Geometry: null,
                    unsupportedReason,
                    measurement,
                    BlocksLowerVisualHits: unsupportedReason is not null));
                continue;
            }

            SvgVisualShapeGeometry? geometry = TryReadGeometry(
                element,
                kind,
                out string? geometryError);
            unsupportedReason ??= geometryError;
            if (positionalError is not null)
            {
                // A transform, visual effect, unsupported ancestor, or invalid
                // viewport makes the raw source geometry unreliable. Fail closed
                // instead of using it as a localized blocker.
                geometry = null;
            }
            elements.Add(new SvgVisualElement(
                element,
                kind,
                geometry,
                unsupportedReason,
                BlocksLowerVisualHits: unsupportedReason is not null));
        }

        return new SvgVisualDocument(viewport, elements);
    }

    private static SvgVisualViewport ReadViewport(
        SvgElementNode root,
        SvgCanvasSize canvasSize,
        out string? error)
    {
        error = null;
        double minX = 0;
        double minY = 0;
        double width = canvasSize.Width;
        double height = canvasSize.Height;
        SvgAttributeSpan? viewBoxAttribute = root.FindAttribute("viewBox");
        if (viewBoxAttribute is not null)
        {
            string[] parts = viewBoxAttribute.RawValue.Split(
                [' ', '\t', '\r', '\n', ','],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
            if (parts.Length == 4
                && TryParseFinite(parts[0], out double parsedMinX)
                && TryParseFinite(parts[1], out double parsedMinY)
                && TryParseFinite(parts[2], out double parsedWidth)
                && TryParseFinite(parts[3], out double parsedHeight)
                && parsedWidth > 0
                && parsedHeight > 0)
            {
                minX = parsedMinX;
                minY = parsedMinY;
                width = parsedWidth;
                height = parsedHeight;
            }
            else
            {
                error =
                    "Visual editing is unavailable because the SVG viewBox is malformed or outside the supported range.";
            }
        }
        else if (!HasSupportedRootLength(root, "width")
            || !HasSupportedRootLength(root, "height"))
        {
            error =
                "Visual editing without a viewBox requires unitless or px canvas dimensions.";
        }

        SvgPreserveAspectRatio preserveAspectRatio =
            ParsePreserveAspectRatio(
                root.FindAttribute("preserveAspectRatio")?.RawValue);
        return new SvgVisualViewport(
            minX,
            minY,
            width,
            height,
            preserveAspectRatio);
    }

    private static bool HasSupportedRootLength(
        SvgElementNode root,
        string name)
    {
        SvgAttributeSpan? attribute = root.FindAttribute(name);
        return attribute is null
            || (SvgVisualLengthParser.TryParse(
                    attribute.RawValue,
                    0,
                    out double value,
                    out _)
                && value > 0);
    }

    private static SvgPreserveAspectRatio ParsePreserveAspectRatio(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SvgPreserveAspectRatio.Default;
        }

        string[] parts = value.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        int offset = parts.Length > 0
            && parts[0].Equals("defer", StringComparison.Ordinal)
                ? 1
                : 0;
        if (offset >= parts.Length)
        {
            return SvgPreserveAspectRatio.Default;
        }

        string alignment = parts[offset];
        if (alignment.Equals("none", StringComparison.Ordinal)
            && parts.Length == offset + 1)
        {
            return new SvgPreserveAspectRatio(
                true,
                0,
                0,
                false,
                "none");
        }

        if (!TryParseAlignment(
                alignment,
                out double alignX,
                out double alignY)
            || parts.Length > offset + 2)
        {
            return SvgPreserveAspectRatio.Default;
        }

        bool isSlice = parts.Length == offset + 2
            && parts[offset + 1].Equals(
                "slice",
                StringComparison.Ordinal);
        if (parts.Length == offset + 2
            && !isSlice
            && !parts[offset + 1].Equals(
                "meet",
                StringComparison.Ordinal))
        {
            return SvgPreserveAspectRatio.Default;
        }

        return new SvgPreserveAspectRatio(
            false,
            alignX,
            alignY,
            isSlice,
            $"{alignment} {(isSlice ? "slice" : "meet")}");
    }

    private static bool TryParseAlignment(
        string value,
        out double alignX,
        out double alignY)
    {
        alignX = 0;
        alignY = 0;
        if (value.Length != 8
            || value[0] != 'x'
            || value[4] != 'Y')
        {
            return false;
        }

        alignX = value[1..4] switch
        {
            "Min" => 0,
            "Mid" => 0.5,
            "Max" => 1,
            _ => double.NaN
        };
        alignY = value[5..] switch
        {
            "Min" => 0,
            "Mid" => 0.5,
            "Max" => 1,
            _ => double.NaN
        };
        return double.IsFinite(alignX) && double.IsFinite(alignY);
    }

    private static string? GetUnsupportedReason(
        SvgElementNode element,
        IReadOnlyDictionary<string, SvgElementNode> elementsByPath)
    {
        foreach (SvgElementNode current in EnumerateSelfAndAncestors(
                     element,
                     elementsByPath))
        {
            if (SvgVisualStylePolicy.HasAmbiguousSyntax(current))
            {
                return "Visual editing is unavailable for ambiguous inline style syntax.";
            }

            if (HasDirectAnimationChild(current))
            {
                return "Visual editing is unavailable for animated elements.";
            }

            if (!ReferenceEquals(current, element)
                && !SupportedAncestorNames.Contains(current.Name))
            {
                return $"Visual editing is unavailable inside {current.Name}.";
            }

            if (HasNonEmptyAttribute(current, "transform")
                || SvgVisualStylePolicy.Defines(current, "transform"))
            {
                return "Visual editing is unavailable for transformed elements or transformed ancestors.";
            }

            if (HasNonEmptyAttribute(current, "clip-path")
                || HasNonEmptyAttribute(current, "mask")
                || HasNonEmptyAttribute(current, "filter")
                || SvgVisualStylePolicy.Defines(current, "clip-path")
                || SvgVisualStylePolicy.Defines(current, "mask")
                || SvgVisualStylePolicy.Defines(current, "filter"))
            {
                return "Visual editing is unavailable for clipped, masked, or filtered geometry.";
            }

            if (HasNonEmptyAttribute(current, "marker")
                || HasNonEmptyAttribute(current, "marker-start")
                || HasNonEmptyAttribute(current, "marker-mid")
                || HasNonEmptyAttribute(current, "marker-end")
                || SvgVisualStylePolicy.Defines(current, "marker")
                || SvgVisualStylePolicy.Defines(current, "marker-start")
                || SvgVisualStylePolicy.Defines(current, "marker-mid")
                || SvgVisualStylePolicy.Defines(current, "marker-end"))
            {
                return "Visual editing is unavailable for marker-decorated geometry.";
            }
        }

        return null;
    }

    private static bool IsInsideNonRenderedContainer(
        SvgElementNode element,
        IReadOnlyDictionary<string, SvgElementNode> elementsByPath)
    {
        int separator = element.StructuralPath.LastIndexOf('/');
        while (separator >= 0)
        {
            string ancestorPath = element.StructuralPath[..separator];
            if (!elementsByPath.TryGetValue(
                    ancestorPath,
                    out SvgElementNode? ancestor))
            {
                return false;
            }
            if (NonRenderedContainerNames.Contains(ancestor.Name))
            {
                return true;
            }
            separator = ancestorPath.LastIndexOf('/');
        }

        return false;
    }

    private static bool HasDirectAnimationChild(SvgElementNode element) =>
        element.Children.Any(child =>
            AnimationElementNames.Contains(child.Name));

    private static SvgVisualShapeGeometry? TryReadUnsupportedBlockerGeometry(
        SvgElementNode element)
    {
        SvgAttributeSpan? attribute = element.Name switch
        {
            "path" => element.FindAttribute("d"),
            "polygon" or "polyline" => element.FindAttribute("points"),
            _ => null
        };
        if (attribute is null
            || !SvgXmlAttributeValueDecoder.TryDecode(
                attribute.RawValue,
                out string decoded))
        {
            return null;
        }

        bool parsed = element.Name == "path"
            ? SvgSimplePathBoundsParser.TryParsePath(
                decoded,
                out SvgVisualBounds bounds)
            : SvgSimplePathBoundsParser.TryParsePoints(
                decoded,
                out bounds);
        if (!parsed)
        {
            return null;
        }

        double expansion = ReadStrokeExpansion(element);
        return new SvgVisualShapeGeometry(
            SvgVisualElementKind.Unsupported,
            bounds.Left - expansion,
            bounds.Top - expansion,
            bounds.Right + expansion,
            bounds.Bottom + expansion);
    }

    private static double ReadStrokeExpansion(SvgElementNode element)
    {
        string? stroke =
            SvgVisualStylePolicy.ReadPresentationValue(element, "stroke");
        if (string.IsNullOrWhiteSpace(stroke)
            || stroke.Trim().Equals(
                "none",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string? strokeWidth =
            SvgVisualStylePolicy.ReadPresentationValue(
                element,
                "stroke-width");
        return SvgVisualLengthParser.TryParse(
                strokeWidth,
                1,
                out double parsed,
                out _)
            && parsed > 0
                ? parsed / 2
                : 0.5;
    }

    private static IEnumerable<SvgElementNode> EnumerateSelfAndAncestors(
        SvgElementNode element,
        IReadOnlyDictionary<string, SvgElementNode> elementsByPath)
    {
        SvgElementNode current = element;
        while (true)
        {
            yield return current;
            int separator = current.StructuralPath.LastIndexOf('/');
            if (separator < 0
                || !elementsByPath.TryGetValue(
                    current.StructuralPath[..separator],
                    out current!))
            {
                yield break;
            }
        }
    }

    private static SvgVisualShapeGeometry? TryReadGeometry(
        SvgElementNode element,
        SvgVisualElementKind kind,
        out string? error)
    {
        error = null;
        string? coordinateError = null;
        bool TryCoordinate(string name, double defaultValue, out double value)
        {
            bool success = SvgVisualLengthParser.TryParse(
                element.FindAttribute(name)?.RawValue,
                defaultValue,
                out value,
                out _);
            if (!success)
            {
                coordinateError =
                    $"Visual editing requires unitless or px {name} geometry.";
            }
            return success;
        }

        switch (kind)
        {
            case SvgVisualElementKind.Rect:
                if (!TryCoordinate("x", 0, out double x)
                    || !TryCoordinate("y", 0, out double y)
                    || !TryCoordinate("width", 0, out double width)
                    || !TryCoordinate("height", 0, out double height))
                {
                    error = coordinateError;
                    return null;
                }
                if (width <= 0 || height <= 0)
                {
                    error = "The rectangle must have positive width and height.";
                    return null;
                }
                return new SvgVisualShapeGeometry(
                    kind,
                    x,
                    y,
                    x + width,
                    y + height);

            case SvgVisualElementKind.Circle:
                if (!TryCoordinate("cx", 0, out double circleX)
                    || !TryCoordinate("cy", 0, out double circleY)
                    || !TryCoordinate("r", 0, out double radius))
                {
                    error = coordinateError;
                    return null;
                }
                if (radius <= 0)
                {
                    error = "The circle must have a positive radius.";
                    return null;
                }
                return new SvgVisualShapeGeometry(
                    kind,
                    circleX - radius,
                    circleY - radius,
                    circleX + radius,
                    circleY + radius);

            case SvgVisualElementKind.Ellipse:
                if (!TryCoordinate("cx", 0, out double ellipseX)
                    || !TryCoordinate("cy", 0, out double ellipseY)
                    || !TryCoordinate("rx", 0, out double radiusX)
                    || !TryCoordinate("ry", 0, out double radiusY))
                {
                    error = coordinateError;
                    return null;
                }
                if (radiusX <= 0 || radiusY <= 0)
                {
                    error =
                        "The ellipse must have positive horizontal and vertical radii.";
                    return null;
                }
                return new SvgVisualShapeGeometry(
                    kind,
                    ellipseX - radiusX,
                    ellipseY - radiusY,
                    ellipseX + radiusX,
                    ellipseY + radiusY);

            case SvgVisualElementKind.Line:
                if (!TryCoordinate("x1", 0, out double x1)
                    || !TryCoordinate("y1", 0, out double y1)
                    || !TryCoordinate("x2", 0, out double x2)
                    || !TryCoordinate("y2", 0, out double y2))
                {
                    error = coordinateError;
                    return null;
                }
                return new SvgVisualShapeGeometry(kind, x1, y1, x2, y2);

            case SvgVisualElementKind.Text:
                error =
                    "Text geometry requires trusted browser measurement.";
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static bool HasNonEmptyAttribute(
        SvgElementNode element,
        string name) =>
        !string.IsNullOrWhiteSpace(element.FindAttribute(name)?.RawValue);

    private static SvgVisualElementKind ParseKind(string name) => name switch
    {
        "rect" => SvgVisualElementKind.Rect,
        "circle" => SvgVisualElementKind.Circle,
        "ellipse" => SvgVisualElementKind.Ellipse,
        "line" => SvgVisualElementKind.Line,
        "text" => SvgVisualElementKind.Text,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static bool TryParseFinite(string text, out double value)
    {
        return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            && double.IsFinite(value)
            && Math.Abs(value) <= SvgVisualLengthParser.MaximumAbsoluteValue;
    }
}
