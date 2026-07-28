using System.Buffers.Binary;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPngPayloadValidator
{
    public static readonly byte[] PngSignature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    public bool IsValid(
        ReadOnlySpan<byte> bytes,
        PreviewPngSize expectedSize)
    {
        if (expectedSize.Width <= 0
            || expectedSize.Width >
                PreviewPngSizeCalculator.MaximumDimension
            || expectedSize.Height <= 0
            || expectedSize.Height >
                PreviewPngSizeCalculator.MaximumDimension
            || expectedSize.PixelCount >
                PreviewPngSizeCalculator.MaximumPixelCount
            || bytes.Length < 45
            || bytes.Length >
                PreviewPngMessageParser.MaximumDecodedPngBytes
            || !bytes[..PngSignature.Length]
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
                    bytes.Slice(offset, 4));
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

            ReadOnlySpan<byte> type = bytes.Slice(offset + 4, 4);
            if (!IsChunkType(type))
            {
                return false;
            }

            if (!foundHeader)
            {
                if (!type.SequenceEqual("IHDR"u8)
                    || length != 13
                    || BinaryPrimitives.ReadUInt32BigEndian(
                        bytes.Slice(dataOffset, 4)) !=
                        (uint)expectedSize.Width
                    || BinaryPrimitives.ReadUInt32BigEndian(
                        bytes.Slice(dataOffset + 4, 4)) !=
                        (uint)expectedSize.Height)
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
            if (!((character >= (byte)'A'
                    && character <= (byte)'Z')
                || (character >= (byte)'a'
                    && character <= (byte)'z')))
            {
                return false;
            }
        }

        return true;
    }
}
