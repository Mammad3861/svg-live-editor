using System.Buffers.Binary;
using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPngMessageParser
{
    public const int MaximumDecodedPngBytes = 40_000_000;
    public const int MaximumBase64Characters = 53_333_336;
    public const int MaximumMessageCharacters =
        MaximumBase64Characters + 1024;

    private static readonly byte[] PngSignature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    public bool TryParse(
        string json,
        PendingPreviewPngCopy expected,
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

            if (!HasExpectedPngStructure(bytes, width, height))
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
        PendingPreviewPngCopy expected)
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

    private static bool HasExpectedPngStructure(
        byte[] bytes,
        int expectedWidth,
        int expectedHeight)
    {
        if (!bytes.AsSpan(0, PngSignature.Length)
                .SequenceEqual(PngSignature))
        {
            return false;
        }

        int offset = PngSignature.Length;
        bool foundHeader = false;
        bool foundImageData = false;
        while (offset <= bytes.Length - 12)
        {
            uint unsignedLength =
                BinaryPrimitives.ReadUInt32BigEndian(
                    bytes.AsSpan(offset, 4));
            if (unsignedLength > int.MaxValue)
            {
                return false;
            }

            int length = (int)unsignedLength;
            int dataOffset = offset + 8;
            if (length > bytes.Length - dataOffset - 4)
            {
                return false;
            }

            ReadOnlySpan<byte> type = bytes.AsSpan(offset + 4, 4);
            if (!IsChunkType(type))
            {
                return false;
            }

            if (!foundHeader)
            {
                if (!type.SequenceEqual("IHDR"u8)
                    || length != 13
                    || BinaryPrimitives.ReadUInt32BigEndian(
                        bytes.AsSpan(dataOffset, 4)) != (uint)expectedWidth
                    || BinaryPrimitives.ReadUInt32BigEndian(
                        bytes.AsSpan(dataOffset + 4, 4)) !=
                        (uint)expectedHeight)
                {
                    return false;
                }

                foundHeader = true;
            }
            else if (type.SequenceEqual("IHDR"u8))
            {
                return false;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                foundImageData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                return foundImageData
                    && length == 0
                    && offset + 12 == bytes.Length;
            }

            offset = dataOffset + length + 4;
        }

        return false;
    }

    private static bool IsChunkType(ReadOnlySpan<byte> type)
    {
        foreach (byte character in type)
        {
            if (!((character >= (byte)'A' && character <= (byte)'Z')
                || (character >= (byte)'a'
                    && character <= (byte)'z')))
            {
                return false;
            }
        }

        return true;
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
