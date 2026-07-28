using System.Buffers.Binary;

namespace SvgLiveEditor.Tests;

internal static class PngTestData
{
    private const string TransparentOnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL3WQAAAABJRU5ErkJggg==";

    public static byte[] CreateDecodedOnePixelPng() =>
        Convert.FromBase64String(TransparentOnePixelPng);

    public static byte[] CreateStructurallyValidPng(
        int width,
        int height)
    {
        byte[] bytes = new byte[57];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(bytes, 0);

        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(8, 4),
            13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(16, 4),
            (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(20, 4),
            (uint)height);
        bytes[24] = 8;
        bytes[25] = 6;

        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(33, 4),
            0);
        "IDAT"u8.CopyTo(bytes.AsSpan(37, 4));

        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(45, 4),
            0);
        "IEND"u8.CopyTo(bytes.AsSpan(49, 4));
        return bytes;
    }
}
