using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewInteractionMessageParser
{
    private const double MaximumViewportDimension = 100_000;
    private const int MaximumImageDimension = 10_000_000;
    private const double MaximumRenderedDimension = 100_000_000;

    public bool TryParseImageLoadState(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        out PreviewImageLoadMessage message)
    {
        message = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 10
                || !TryReadString(root, "type", out string? type)
                || type != "imageState"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !root.TryGetProperty(
                    "sourceRevision",
                    out JsonElement sourceRevisionProperty)
                || sourceRevisionProperty.ValueKind != JsonValueKind.Number
                || !sourceRevisionProperty.TryGetInt64(out long sourceRevision)
                || sourceRevision != expectedSourceRevision
                || !TryReadString(root, "state", out string? stateText)
                || !TryReadInteger(
                    root,
                    "naturalWidth",
                    0,
                    MaximumImageDimension,
                    out int naturalWidth)
                || !TryReadInteger(
                    root,
                    "naturalHeight",
                    0,
                    MaximumImageDimension,
                    out int naturalHeight)
                || !TryReadNumber(
                    root,
                    "renderedWidth",
                    0,
                    MaximumRenderedDimension,
                    out double renderedWidth)
                || !TryReadNumber(
                    root,
                    "renderedHeight",
                    0,
                    MaximumRenderedDimension,
                    out double renderedHeight)
                || !TryReadNumber(
                    root,
                    "viewportWidth",
                    0,
                    MaximumViewportDimension,
                    out double viewportWidth)
                || !TryReadNumber(
                    root,
                    "viewportHeight",
                    0,
                    MaximumViewportDimension,
                    out double viewportHeight))
            {
                return false;
            }

            PreviewImageLoadState state;
            if (stateText == "loaded"
                && naturalWidth > 0
                && naturalHeight > 0
                && renderedWidth > 0
                && renderedHeight > 0
                && viewportWidth > 0
                && viewportHeight > 0)
            {
                state = PreviewImageLoadState.Loaded;
            }
            else if (stateText == "error"
                && naturalWidth == 0
                && naturalHeight == 0
                && renderedWidth == 0
                && renderedHeight == 0
                && viewportWidth == 0
                && viewportHeight == 0)
            {
                state = PreviewImageLoadState.Error;
            }
            else
            {
                return false;
            }

            message = new PreviewImageLoadMessage(
                state,
                sourceRevision,
                naturalWidth,
                naturalHeight,
                renderedWidth,
                renderedHeight,
                viewportWidth,
                viewportHeight);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseZoomRequest(
        string json,
        string expectedToken,
        out PreviewZoomRequest request)
    {
        request = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 9
                || !TryReadString(root, "type", out string? type)
                || type != "zoom"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !TryReadString(root, "direction", out string? directionText)
                || !TryReadNumber(root, "contentX", 0, 1, out double contentX)
                || !TryReadNumber(root, "contentY", 0, 1, out double contentY)
                || !TryReadNumber(root, "anchorX", 0, MaximumViewportDimension, out double anchorX)
                || !TryReadNumber(root, "anchorY", 0, MaximumViewportDimension, out double anchorY)
                || !TryReadNumber(root, "viewportWidth", 1, MaximumViewportDimension, out double viewportWidth)
                || !TryReadNumber(root, "viewportHeight", 1, MaximumViewportDimension, out double viewportHeight)
                || anchorX > viewportWidth
                || anchorY > viewportHeight)
            {
                return false;
            }

            PreviewZoomDirection direction;
            if (directionText == "in")
            {
                direction = PreviewZoomDirection.In;
            }
            else if (directionText == "out")
            {
                direction = PreviewZoomDirection.Out;
            }
            else
            {
                return false;
            }

            request = new PreviewZoomRequest(
                direction,
                contentX,
                contentY,
                anchorX,
                anchorY,
                viewportWidth,
                viewportHeight);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseViewportPosition(
        string json,
        string expectedToken,
        out PreviewViewportPosition viewport)
    {
        viewport = PreviewViewportPosition.Center;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 4
                || !TryReadString(root, "type", out string? type)
                || type != "viewport"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !TryReadNumber(root, "centerX", 0, 1, out double centerX)
                || !TryReadNumber(root, "centerY", 0, 1, out double centerY))
            {
                return false;
            }

            viewport = new PreviewViewportPosition(centerX, centerY);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParsePanCommand(
        string json,
        string expectedToken,
        out PreviewPanCommand command)
    {
        command = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 3
                || !TryReadString(root, "type", out string? type)
                || type != "panCommand"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(
                    token,
                    expectedToken,
                    StringComparison.Ordinal)
                || !TryReadString(root, "command", out string? commandText))
            {
                return false;
            }

            if (commandText == "toggle")
            {
                command = PreviewPanCommand.Toggle;
                return true;
            }

            if (commandText == "exit")
            {
                command = PreviewPanCommand.Exit;
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseContextMenuRequest(
        string json,
        string expectedToken,
        out PreviewContextMenuRequest request)
    {
        request = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 8
                || !TryReadString(root, "type", out string? type)
                || type != "contextMenu"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !TryReadNumber(root, "x", 0, MaximumViewportDimension, out double x)
                || !TryReadNumber(root, "y", 0, MaximumViewportDimension, out double y)
                || !TryReadNumber(
                    root,
                    "viewportWidth",
                    1,
                    MaximumViewportDimension,
                    out double viewportWidth)
                || !TryReadNumber(
                    root,
                    "viewportHeight",
                    1,
                    MaximumViewportDimension,
                    out double viewportHeight)
                || !root.TryGetProperty("sourceRevision", out JsonElement revisionProperty)
                || revisionProperty.ValueKind != JsonValueKind.Number
                || !revisionProperty.TryGetInt64(out long sourceRevision)
                || sourceRevision < 0
                || !TryReadString(root, "selectionId", out string? selectionId)
                || selectionId!.Length is not 0 and not 32
                || (selectionId.Length == 32 && !selectionId.All(Uri.IsHexDigit))
                || x > viewportWidth
                || y > viewportHeight)
            {
                return false;
            }

            request = new PreviewContextMenuRequest(
                x,
                y,
                viewportWidth,
                viewportHeight,
                sourceRevision,
                selectionId);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool IsCopyCommand(string json, string expectedToken)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.EnumerateObject().Count() == 2
                && TryReadString(root, "type", out string? type)
                && type == "copyCommand"
                && TryReadString(root, "token", out string? token)
                && string.Equals(token, expectedToken, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseAuthoringCommand(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        out PreviewAuthoringCommand command)
    {
        command = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 4
                || !TryReadString(root, "type", out string? type)
                || type != "authoringCommand"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !root.TryGetProperty(
                    "sourceRevision",
                    out JsonElement revisionProperty)
                || revisionProperty.ValueKind != JsonValueKind.Number
                || !revisionProperty.TryGetInt64(out long sourceRevision)
                || sourceRevision != expectedSourceRevision
                || !TryReadString(root, "command", out string? commandText))
            {
                return false;
            }

            if (commandText == "delete")
            {
                command = PreviewAuthoringCommand.Delete;
                return true;
            }
            if (commandText == "duplicate")
            {
                command = PreviewAuthoringCommand.Duplicate;
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseDirectDragArmRequest(
        string json,
        string expectedToken,
        out PreviewDirectDragArmRequest request)
    {
        request = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 17
                || !TryReadString(root, "type", out string? type)
                || type != "directDrag"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !TryReadString(root, "action", out string? action)
                || action != "arm"
                || !TryReadHexId(root, "gestureId", out string? gestureId)
                || !TryReadPointerCoordinates(
                    root,
                    out double x,
                    out double y,
                    out double viewportWidth,
                    out double viewportHeight)
                || !TryReadInteger(root, "button", 0, 2, out int button)
                || !TryReadBoolean(root, "startedOnArtwork", out bool startedOnArtwork)
                || !TryReadBoolean(root, "isPrimary", out bool isPrimary)
                || !TryReadString(root, "pointerType", out string? pointerType)
                || pointerType != "mouse"
                || !TryReadBoolean(root, "ctrlKey", out bool controlHeld)
                || !TryReadBoolean(root, "shiftKey", out bool shiftHeld)
                || !TryReadBoolean(root, "altKey", out bool altHeld)
                || !TryReadBoolean(root, "metaKey", out bool metaHeld)
                || !TryReadBoolean(root, "spaceHeld", out bool isSpaceHeld))
            {
                return false;
            }

            request = new PreviewDirectDragArmRequest(
                gestureId!,
                new PreviewPointerGestureInput(
                    button,
                    startedOnArtwork,
                    isPrimary,
                    IsMouse: true,
                    controlHeld,
                    shiftHeld,
                    altHeld,
                    metaHeld,
                    isSpaceHeld,
                    PanModeEnabled: false),
                x,
                y,
                viewportWidth,
                viewportHeight);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool TryParseDirectDragSignal(
        string json,
        string expectedToken,
        out PreviewDirectDragSignal signal)
    {
        signal = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 8
                || !TryReadString(root, "type", out string? type)
                || type != "directDrag"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(token, expectedToken, StringComparison.Ordinal)
                || !TryReadString(root, "action", out string? actionText)
                || !TryReadHexId(root, "gestureId", out string? gestureId)
                || !TryReadPointerCoordinates(
                    root,
                    out double x,
                    out double y,
                    out double viewportWidth,
                    out double viewportHeight))
            {
                return false;
            }

            PreviewDirectDragSignalAction action;
            if (actionText == "start")
            {
                action = PreviewDirectDragSignalAction.Start;
            }
            else if (actionText == "cancel")
            {
                action = PreviewDirectDragSignalAction.Cancel;
            }
            else
            {
                return false;
            }

            signal = new PreviewDirectDragSignal(
                action,
                gestureId!,
                x,
                y,
                viewportWidth,
                viewportHeight);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadString(
        JsonElement root,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return value is not null;
    }

    private static bool TryReadHexId(
        JsonElement root,
        string propertyName,
        out string? value)
    {
        return TryReadString(root, propertyName, out value)
            && value!.Length == 32
            && value.All(Uri.IsHexDigit);
    }

    private static bool TryReadBoolean(
        JsonElement root,
        string propertyName,
        out bool value)
    {
        value = false;
        if (!root.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadInteger(
        JsonElement root,
        string propertyName,
        int minimum,
        int maximum,
        out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value >= minimum
            && value <= maximum;
    }

    private static bool TryReadPointerCoordinates(
        JsonElement root,
        out double x,
        out double y,
        out double viewportWidth,
        out double viewportHeight)
    {
        x = 0;
        y = 0;
        viewportWidth = 0;
        viewportHeight = 0;
        return TryReadNumber(
                root,
                "x",
                0,
                MaximumViewportDimension,
                out x)
            && TryReadNumber(
                root,
                "y",
                0,
                MaximumViewportDimension,
                out y)
            && TryReadNumber(
                root,
                "viewportWidth",
                1,
                MaximumViewportDimension,
                out viewportWidth)
            && TryReadNumber(
                root,
                "viewportHeight",
                1,
                MaximumViewportDimension,
                out viewportHeight)
            && x <= viewportWidth
            && y <= viewportHeight;
    }

    private static bool TryReadNumber(
        JsonElement root,
        string propertyName,
        double minimum,
        double maximum,
        out double value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value)
            && double.IsFinite(value)
            && value >= minimum
            && value <= maximum;
    }
}
