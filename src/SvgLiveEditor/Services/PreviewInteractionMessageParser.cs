using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewInteractionMessageParser
{
    private const double MaximumViewportDimension = 100_000;

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
