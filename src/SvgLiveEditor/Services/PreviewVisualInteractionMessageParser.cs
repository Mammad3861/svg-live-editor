using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewVisualInteractionMessageParser
{
    private const double MaximumViewportDimension = 100_000;
    private const double MaximumImageDimension = 10_000_000;
    private const double MaximumImageOffset = 10_000_000;
    private const long MaximumSourceRevision = 9_007_199_254_740_991;

    public bool TryParsePointer(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        out PreviewVisualPointerMessage message)
    {
        message = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 22
                || !ReadString(root, "type", out string? type)
                || type != "visualPointer"
                || !ReadString(root, "token", out string? token)
                || !string.Equals(
                    token,
                    expectedToken,
                    StringComparison.Ordinal)
                || !ReadRevision(
                    root,
                    "sourceRevision",
                    expectedSourceRevision,
                    out long sourceRevision)
                || !ReadString(root, "phase", out string? phaseText)
                || !TryParsePhase(
                    phaseText,
                    out PreviewVisualPointerPhase phase)
                || !ReadHexId(root, "gestureId", out string? gestureId)
                || !ReadNumber(
                    root,
                    "x",
                    0,
                    MaximumViewportDimension,
                    out double x)
                || !ReadNumber(
                    root,
                    "y",
                    0,
                    MaximumViewportDimension,
                    out double y)
                || !ReadNumber(
                    root,
                    "viewportWidth",
                    1,
                    MaximumViewportDimension,
                    out double viewportWidth)
                || !ReadNumber(
                    root,
                    "viewportHeight",
                    1,
                    MaximumViewportDimension,
                    out double viewportHeight)
                || x > viewportWidth
                || y > viewportHeight
                || !ReadNumber(
                    root,
                    "imageLeft",
                    -MaximumImageOffset,
                    MaximumImageOffset,
                    out double imageLeft)
                || !ReadNumber(
                    root,
                    "imageTop",
                    -MaximumImageOffset,
                    MaximumImageOffset,
                    out double imageTop)
                || !ReadNumber(
                    root,
                    "imageWidth",
                    double.Epsilon,
                    MaximumImageDimension,
                    out double imageWidth)
                || !ReadNumber(
                    root,
                    "imageHeight",
                    double.Epsilon,
                    MaximumImageDimension,
                    out double imageHeight)
                || !ReadInteger(root, "button", 0, 2, out int button)
                || !ReadInteger(root, "buttons", 0, 31, out int buttons)
                || !ReadBoolean(root, "ctrlKey", out bool controlHeld)
                || !ReadBoolean(root, "shiftKey", out bool shiftHeld)
                || !ReadBoolean(root, "altKey", out bool altHeld)
                || !ReadBoolean(root, "metaKey", out bool metaHeld)
                || !ReadBoolean(root, "spaceHeld", out bool spaceHeld)
                || !ReadString(root, "pointerType", out string? pointerType)
                || pointerType != "mouse"
                || !ReadBoolean(root, "isPrimary", out bool isPrimary)
                || !isPrimary)
            {
                return false;
            }

            message = new PreviewVisualPointerMessage(
                phase,
                gestureId!,
                sourceRevision,
                new SvgVisualPoint(x, y),
                viewportWidth,
                viewportHeight,
                new PreviewImageMetrics(
                    imageLeft,
                    imageTop,
                    imageWidth,
                    imageHeight),
                button,
                buttons,
                controlHeld,
                shiftHeld,
                altHeld,
                metaHeld,
                spaceHeld);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseNudge(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        out PreviewVisualNudgeRequest request)
    {
        request = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 5
                || !ReadString(root, "type", out string? type)
                || type != "visualNudge"
                || !ReadString(root, "token", out string? token)
                || !string.Equals(
                    token,
                    expectedToken,
                    StringComparison.Ordinal)
                || !ReadRevision(
                    root,
                    "sourceRevision",
                    expectedSourceRevision,
                    out long sourceRevision)
                || !ReadNumber(root, "deltaX", -10, 10, out double deltaX)
                || !ReadNumber(root, "deltaY", -10, 10, out double deltaY)
                || !IsSupportedNudge(deltaX, deltaY))
            {
                return false;
            }

            request = new PreviewVisualNudgeRequest(
                sourceRevision,
                deltaX,
                deltaY);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseResizePointer(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        string expectedSelectionId,
        out PreviewVisualResizePointerMessage message)
    {
        message = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 25
                || !ReadString(root, "type", out string? type)
                || type != "visualResizePointer"
                || !ReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !ReadRevision(
                    root,
                    "sourceRevision",
                    expectedSourceRevision,
                    out long sourceRevision)
                || !ReadHexId(root, "selectionId", out string? selectionId)
                || !string.Equals(
                    selectionId,
                    expectedSelectionId,
                    StringComparison.Ordinal)
                || !ReadString(root, "phase", out string? phaseText)
                || !TryParsePhase(
                    phaseText,
                    out PreviewVisualPointerPhase phase)
                || !ReadHexId(root, "gestureId", out string? gestureId)
                || !ReadString(root, "handle", out string? handleText)
                || !SvgVisualResizeHandleService.TryParseWireName(
                    handleText,
                    out SvgResizeHandle handle)
                || !ReadNumber(
                    root,
                    "x",
                    0,
                    MaximumViewportDimension,
                    out double x)
                || !ReadNumber(
                    root,
                    "y",
                    0,
                    MaximumViewportDimension,
                    out double y)
                || !ReadNumber(
                    root,
                    "viewportWidth",
                    1,
                    MaximumViewportDimension,
                    out double viewportWidth)
                || !ReadNumber(
                    root,
                    "viewportHeight",
                    1,
                    MaximumViewportDimension,
                    out double viewportHeight)
                || x > viewportWidth
                || y > viewportHeight
                || !ReadNumber(
                    root,
                    "imageLeft",
                    -MaximumImageOffset,
                    MaximumImageOffset,
                    out double imageLeft)
                || !ReadNumber(
                    root,
                    "imageTop",
                    -MaximumImageOffset,
                    MaximumImageOffset,
                    out double imageTop)
                || !ReadNumber(
                    root,
                    "imageWidth",
                    double.Epsilon,
                    MaximumImageDimension,
                    out double imageWidth)
                || !ReadNumber(
                    root,
                    "imageHeight",
                    double.Epsilon,
                    MaximumImageDimension,
                    out double imageHeight)
                || !ReadInteger(root, "button", 0, 2, out int button)
                || !ReadInteger(root, "buttons", 0, 31, out int buttons)
                || !ReadBoolean(root, "ctrlKey", out bool controlHeld)
                || !ReadBoolean(root, "shiftKey", out bool shiftHeld)
                || !ReadBoolean(root, "altKey", out bool altHeld)
                || !ReadBoolean(root, "metaKey", out bool metaHeld)
                || !ReadBoolean(root, "spaceHeld", out bool spaceHeld)
                || !ReadString(root, "pointerType", out string? pointerType)
                || pointerType != "mouse"
                || !ReadBoolean(root, "isTrusted", out bool isTrusted)
                || !isTrusted
                || !ReadBoolean(root, "isPrimary", out bool isPrimary)
                || !isPrimary)
            {
                return false;
            }

            message = new PreviewVisualResizePointerMessage(
                phase,
                gestureId!,
                selectionId!,
                handle,
                sourceRevision,
                new SvgVisualPoint(x, y),
                viewportWidth,
                viewportHeight,
                new PreviewImageMetrics(
                    imageLeft,
                    imageTop,
                    imageWidth,
                    imageHeight),
                button,
                buttons,
                controlHeld,
                shiftHeld,
                altHeld,
                metaHeld,
                spaceHeld);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsSupportedNudge(double deltaX, double deltaY)
    {
        bool validX = deltaX is -10 or -1 or 0 or 1 or 10;
        bool validY = deltaY is -10 or -1 or 0 or 1 or 10;
        return validX
            && validY
            && (deltaX == 0) != (deltaY == 0);
    }

    private static bool TryParsePhase(
        string? value,
        out PreviewVisualPointerPhase phase)
    {
        phase = value switch
        {
            "down" => PreviewVisualPointerPhase.Down,
            "move" => PreviewVisualPointerPhase.Move,
            "up" => PreviewVisualPointerPhase.Up,
            "cancel" => PreviewVisualPointerPhase.Cancel,
            _ => default
        };
        return value is "down" or "move" or "up" or "cancel";
    }

    private static bool ReadRevision(
        JsonElement root,
        string name,
        long expected,
        out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value)
            && value >= 0
            && value <= MaximumSourceRevision
            && value == expected;
    }

    private static bool ReadString(
        JsonElement root,
        string name,
        out string? value)
    {
        value = null;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            && (value = property.GetString()) is not null;
    }

    private static bool ReadHexId(
        JsonElement root,
        string name,
        out string? value)
    {
        return ReadString(root, name, out value)
            && value!.Length == 32
            && value.All(Uri.IsHexDigit);
    }

    private static bool ReadBoolean(
        JsonElement root,
        string name,
        out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool ReadInteger(
        JsonElement root,
        string name,
        int minimum,
        int maximum,
        out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value >= minimum
            && value <= maximum;
    }

    private static bool ReadNumber(
        JsonElement root,
        string name,
        double minimum,
        double maximum,
        out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value)
            && double.IsFinite(value)
            && value >= minimum
            && value <= maximum;
    }
}
