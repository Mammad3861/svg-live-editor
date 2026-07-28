using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPngMessageParser
{
    public const int MaximumDecodedPngBytes = 40_000_000;
    public const int MaximumBase64Characters = 53_333_336;
    public const int MaximumMessageCharacters =
        MaximumBase64Characters + 1024;

    private readonly PreviewPngPayloadValidator _payloadValidator;

    public PreviewPngMessageParser(
        PreviewPngPayloadValidator? payloadValidator = null)
    {
        _payloadValidator =
            payloadValidator ?? new PreviewPngPayloadValidator();
    }

    public bool TryParse(
        string json,
        PendingPreviewPngRequest expected,
        out PreviewPngPayload? payload)
    {
        payload = null;
        if (string.IsNullOrEmpty(json)
            || json.Length > MaximumMessageCharacters)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 7
                || !TryReadString(root, "type", out string? type)
                || type != "png"
                || !TryReadString(root, "token", out string? token)
                || !string.Equals(
                    token,
                    expected.BridgeToken,
                    StringComparison.Ordinal)
                || !TryReadString(root, "requestId", out string? requestId)
                || !string.Equals(
                    requestId,
                    expected.RequestId,
                    StringComparison.Ordinal)
                || !TryReadString(root, "mimeType", out string? mimeType)
                || mimeType != "image/png"
                || !TryReadInt32(root, "width", out int width)
                || !TryReadInt32(root, "height", out int height)
                || width != expected.Plan.Size.Width
                || height != expected.Plan.Size.Height
                || (long)width * height > PreviewPngSizeCalculator.MaximumPixelCount
                || !TryReadString(root, "payload", out string? encoded)
                || encoded is not string encodedValue
                || encodedValue.Length == 0
                || encodedValue.Length > MaximumBase64Characters
                || encodedValue.Length % 4 != 0)
            {
                return false;
            }

            int maximumDecodedLength =
                (encodedValue.Length / 4) * 3;
            if (maximumDecodedLength > MaximumDecodedPngBytes)
            {
                return false;
            }

            byte[] bytes = new byte[maximumDecodedLength];
            if (!Convert.TryFromBase64String(
                    encodedValue,
                    bytes,
                    out int bytesWritten)
                || bytesWritten < 45
                || bytesWritten > MaximumDecodedPngBytes)
            {
                return false;
            }

            if (bytesWritten != bytes.Length)
            {
                Array.Resize(ref bytes, bytesWritten);
            }

            if (!_payloadValidator.IsValid(
                    bytes,
                    new PreviewPngSize(width, height)))
            {
                return false;
            }

            payload = new PreviewPngPayload(
                new PreviewPngSize(width, height),
                bytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool IsMatchingError(
        string json,
        PendingPreviewPngRequest expected)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 1024)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.EnumerateObject().Count() == 3
                && TryReadString(root, "type", out string? type)
                && type == "pngError"
                && TryReadString(root, "token", out string? token)
                && string.Equals(
                    token,
                    expected.BridgeToken,
                    StringComparison.Ordinal)
                && TryReadString(root, "requestId", out string? requestId)
                && string.Equals(
                    requestId,
                    expected.RequestId,
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadString(
        JsonElement root,
        string name,
        out string? value)
    {
        value = null;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            && (value = property.GetString()) is not null;
    }

    private static bool TryReadInt32(
        JsonElement root,
        string name,
        out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value > 0
            && value <= PreviewPngSizeCalculator.MaximumDimension;
    }
}
