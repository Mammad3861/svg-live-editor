using System.Drawing;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ApplicationIconTests
{
    private static readonly int[] RequiredSizes = [16, 24, 32, 48, 64, 128, 256];

    [TestMethod]
    public void Ico_ContainsValidWindowsCompatibleFrames()
    {
        IReadOnlyList<IconFrame> frames = WindowsIconTestSupport.ReadIconFile(
            WindowsIconTestSupport.GetSourceIconPath());

        CollectionAssert.AreEqual(
            RequiredSizes,
            frames.Select(frame => frame.Width).ToArray());
        foreach (IconFrame frame in frames)
        {
            Assert.AreEqual(frame.Width, frame.Height);
            Assert.AreEqual(
                frame.Width == 256 ? (byte)0 : checked((byte)frame.Width),
                frame.DirectoryWidth);
            Assert.AreEqual(
                frame.Height == 256 ? (byte)0 : checked((byte)frame.Height),
                frame.DirectoryHeight);
            Assert.AreEqual((ushort)1, frame.Planes);
            Assert.AreEqual((ushort)32, frame.BitDepth);
            Assert.AreEqual(
                frame.Width == 256
                    ? IconFrameEncoding.Png
                    : IconFrameEncoding.Dib,
                frame.Encoding,
                $"Unexpected encoding for the {frame.Width}px frame.");

            using Bitmap decoded = WindowsIconTestSupport.DecodeFrame(frame);
            Assert.AreEqual(frame.Width, decoded.Width);
            Assert.AreEqual(frame.Height, decoded.Height);
            if (frame.Encoding == IconFrameEncoding.Dib)
            {
                AssertDibStructureAndPixels(frame, decoded);
            }

            int minimumAlpha = 255;
            int maximumAlpha = 0;
            for (int y = 0; y < decoded.Height; y++)
            {
                for (int x = 0; x < decoded.Width; x++)
                {
                    int alpha = decoded.GetPixel(x, y).A;
                    minimumAlpha = Math.Min(minimumAlpha, alpha);
                    maximumAlpha = Math.Max(maximumAlpha, alpha);
                }
            }
            Assert.AreEqual(0, minimumAlpha);
            Assert.AreEqual(255, maximumAlpha);
        }
    }

    [TestMethod]
    public void ReleaseExecutable_EmbedsAndExposesTheIntendedApplicationIcon()
    {
        string builtExecutablePath = Path.Combine(
            Path.GetDirectoryName(typeof(MainWindow).Assembly.Location)!,
            "SvgLiveEditor.exe");
        string sourceIconPath = WindowsIconTestSupport.GetSourceIconPath();
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.IconAudit.Tests",
            Guid.NewGuid().ToString("N"));
        string executablePath = Path.Combine(
            temporaryDirectory,
            "SvgLiveEditor.exe");

        Assert.IsTrue(File.Exists(builtExecutablePath));
        Directory.CreateDirectory(temporaryDirectory);
        File.Copy(builtExecutablePath, executablePath);
        try
        {
            IReadOnlyList<IconFrame> sourceFrames =
                WindowsIconTestSupport.ReadIconFile(sourceIconPath);
            IReadOnlyList<ExecutableIconFrame> executableFrames =
                WindowsIconTestSupport.ReadExecutableIconFrames(executablePath);

            Assert.AreEqual(sourceFrames.Count, executableFrames.Count);
            for (int index = 0; index < sourceFrames.Count; index++)
            {
                IconFrame source = sourceFrames[index];
                ExecutableIconFrame embedded = executableFrames[index];
                Assert.AreEqual(source.Width, embedded.Width);
                Assert.AreEqual(source.Height, embedded.Height);
                Assert.AreEqual(source.Planes, embedded.Planes);
                Assert.AreEqual(source.BitDepth, embedded.BitDepth);
                Assert.AreEqual(source.Encoding, embedded.Encoding);
                CollectionAssert.AreEqual(source.Payload, embedded.Payload);
            }

            using Icon? associated = Icon.ExtractAssociatedIcon(executablePath);
            Assert.IsNotNull(associated);
            Assert.IsTrue(
                WindowsIconTestSupport.ContainsSvgLiveEditorBrandPalette(
                    associated),
                "ExtractAssociatedIcon did not expose the distinctive "
                    + "SvgLiveEditor artwork and palette.");
            Assert.IsFalse(
                WindowsIconTestSupport.ContainsSvgLiveEditorBrandPalette(
                    SystemIcons.Application),
                "The custom-palette check must reject the generic Windows icon.");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void AssertDibStructureAndPixels(
        IconFrame frame,
        Bitmap decoded)
    {
        byte[] payload = frame.Payload;
        Assert.IsTrue(payload.Length >= 40);
        Assert.AreEqual((uint)40, BitConverter.ToUInt32(payload, 0));
        Assert.AreEqual(frame.Width, BitConverter.ToInt32(payload, 4));
        Assert.AreEqual(frame.Height * 2, BitConverter.ToInt32(payload, 8));
        Assert.AreEqual((ushort)1, BitConverter.ToUInt16(payload, 12));
        Assert.AreEqual((ushort)32, BitConverter.ToUInt16(payload, 14));
        Assert.AreEqual((uint)0, BitConverter.ToUInt32(payload, 16));
        Assert.AreEqual(0, BitConverter.ToInt32(payload, 24));
        Assert.AreEqual(0, BitConverter.ToInt32(payload, 28));
        Assert.AreEqual((uint)0, BitConverter.ToUInt32(payload, 32));
        Assert.AreEqual((uint)0, BitConverter.ToUInt32(payload, 36));

        int xorStride = checked(frame.Width * 4);
        Assert.AreEqual(0, xorStride % 4);
        int xorLength = checked(xorStride * frame.Height);
        int maskStride = ((frame.Width + 31) / 32) * 4;
        Assert.AreEqual(0, maskStride % 4);
        Assert.AreEqual((uint)xorLength, BitConverter.ToUInt32(payload, 20));
        Assert.AreEqual(
            40 + xorLength + (maskStride * frame.Height),
            payload.Length);

        int opaquePixelCount = 0;
        int maskOffset = 40 + xorLength;
        for (int y = 0; y < frame.Height; y++)
        {
            int storedRow = frame.Height - 1 - y;
            for (int x = 0; x < frame.Width; x++)
            {
                int pixelOffset = 40 + (storedRow * xorStride) + (x * 4);
                Color decodedPixel = decoded.GetPixel(x, y);
                Assert.AreEqual(decodedPixel.A, payload[pixelOffset + 3]);
                if (decodedPixel.A == 255)
                {
                    opaquePixelCount++;
                    Assert.AreEqual(decodedPixel.B, payload[pixelOffset]);
                    Assert.AreEqual(decodedPixel.G, payload[pixelOffset + 1]);
                    Assert.AreEqual(decodedPixel.R, payload[pixelOffset + 2]);
                }

                bool maskBit = ReadMaskBit(
                    payload,
                    maskOffset + (storedRow * maskStride),
                    x);
                Assert.AreEqual(
                    decodedPixel.A == 0,
                    maskBit,
                    $"Unexpected AND-mask bit at {frame.Width}px ({x}, {y}).");
            }

            for (int x = frame.Width; x < maskStride * 8; x++)
            {
                Assert.IsFalse(ReadMaskBit(
                    payload,
                    maskOffset + (storedRow * maskStride),
                    x));
            }
        }
        Assert.IsTrue(opaquePixelCount > 0);
    }

    private static bool ReadMaskBit(byte[] payload, int rowOffset, int x) =>
        (payload[rowOffset + (x / 8)] & (0x80 >> (x % 8))) != 0;
}
