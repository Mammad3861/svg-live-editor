using System.Buffers.Binary;
using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewPngCopyTests
{
    private const string BridgeToken =
        "00112233445566778899AABBCCDDEEFF";
    private const string RequestId =
        "FFEEDDCCBBAA99887766554433221100";

    private readonly PreviewPngSizeCalculator _calculator = new();
    private readonly SvgCanvasSizeReader _canvasSizeReader = new();
    private readonly PreviewPngMessageParser _parser = new();

    [TestMethod]
    public void OutputSize_UsesIntrinsicWidthAndHeight()
    {
        const string svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1040\" height=\"440\" />";

        PreviewPngSize result =
            _calculator.Calculate(_canvasSizeReader.Read(svg));

        Assert.AreEqual(new PreviewPngSize(1040, 440), result);
    }

    [TestMethod]
    public void OutputSize_UsesViewBoxOnlyDimensions()
    {
        const string svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 800 320\" />";

        PreviewPngSize result =
            _calculator.Calculate(_canvasSizeReader.Read(svg));

        Assert.AreEqual(new PreviewPngSize(800, 320), result);
    }

    [TestMethod]
    public void OutputSize_DownscalesOversizedArtworkWithinBothLimits()
    {
        PreviewPngSize result =
            _calculator.Calculate(new SvgCanvasSize(20_000, 10_000));

        Assert.IsTrue(
            result.Width <= PreviewPngSizeCalculator.MaximumDimension);
        Assert.IsTrue(
            result.Height <= PreviewPngSizeCalculator.MaximumDimension);
        Assert.IsTrue(
            result.PixelCount <= PreviewPngSizeCalculator.MaximumPixelCount);
        Assert.AreEqual(2.0, result.Width / (double)result.Height, 0.001);
    }

    [TestMethod]
    [DataRow(0, 100)]
    [DataRow(-1, 100)]
    [DataRow(double.NaN, 100)]
    [DataRow(double.PositiveInfinity, 100)]
    [DataRow(100, 0)]
    [DataRow(100, double.NegativeInfinity)]
    [DataRow(1_000_000_001, 100)]
    public void OutputSize_RejectsInvalidOrUnreasonableDimensions(
        double width,
        double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Calculate(new SvgCanvasSize(width, height)));
    }

    [TestMethod]
    public void Policy_UsesLastValidPreviewForInvalidCurrentSource()
    {
        PreviewPngCopyPolicy policy = new();

        bool created = policy.TryCreatePlan(
            hasVisiblePreview: true,
            PreviewPngSourceState.CurrentInvalid,
            new SvgCanvasSize(1040, 440),
            out PreviewPngCopyPlan? plan);

        Assert.IsTrue(created);
        Assert.IsNotNull(plan);
        Assert.IsTrue(plan.UsesLastValidPreview);
        Assert.AreEqual(new PreviewPngSize(1040, 440), plan.Size);
    }

    [TestMethod]
    public void Policy_FailsWhenNoValidPreviewExists()
    {
        PreviewPngCopyPolicy policy = new();

        Assert.IsFalse(policy.TryCreatePlan(
            hasVisiblePreview: false,
            PreviewPngSourceState.CurrentInvalid,
            lastValidCanvasSize: null,
            out PreviewPngCopyPlan? plan));
        Assert.IsNull(plan);
    }

    [TestMethod]
    public void Policy_DistinguishesPendingValidationFromInvalidSource()
    {
        PreviewPngCopyPolicy policy = new();

        Assert.IsTrue(policy.TryCreatePlan(
            hasVisiblePreview: true,
            PreviewPngSourceState.PendingValidation,
            new SvgCanvasSize(300, 150),
            out PreviewPngCopyPlan? plan));

        Assert.IsNotNull(plan);
        Assert.AreEqual(
            PreviewPngSourceState.PendingValidation,
            plan.SourceState);
        Assert.IsTrue(plan.UsesLastValidPreview);
    }

    [TestMethod]
    public void Policy_DoesNotMutateDocumentZoomOrViewportState()
    {
        PreviewZoomState zoom = new(
            PreviewZoomMode.Manual,
            ManualScale: 2);
        PreviewViewportPosition viewport = new(0.8, 0.2);
        bool modified = true;
        PreviewPngCopyPolicy policy = new();

        Assert.IsTrue(policy.TryCreatePlan(
            hasVisiblePreview: true,
            PreviewPngSourceState.CurrentValid,
            new SvgCanvasSize(300, 150),
            out _));

        Assert.AreEqual("200%", zoom.DisplayText);
        Assert.AreEqual(new PreviewViewportPosition(0.8, 0.2), viewport);
        Assert.IsTrue(modified);
    }

    [TestMethod]
    public void PngMessage_AcceptsOnlyExpectedTokenSchemaDimensionsAndSignature()
    {
        byte[] png = CreateStructurallyValidPng(1, 1);
        PendingPreviewPngCopy expected = CreatePending(
            new PreviewPngSize(1, 1));
        string json = CreateMessage(
            png,
            BridgeToken,
            RequestId,
            width: 1,
            height: 1);

        Assert.IsTrue(_parser.TryParse(
            json,
            expected,
            out PreviewPngPayload? payload));
        Assert.IsNotNull(payload);
        Assert.AreEqual(new PreviewPngSize(1, 1), payload.Size);
        CollectionAssert.AreEqual(png, payload.Bytes);
    }

    [TestMethod]
    public void PngMessage_RejectsStaleTokensExtraFieldsWrongMimeAndDimensions()
    {
        byte[] png = CreateStructurallyValidPng(1, 1);
        PendingPreviewPngCopy expected = CreatePending(
            new PreviewPngSize(1, 1));
        string stale = CreateMessage(
            png,
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            RequestId,
            1,
            1);
        string wrongMime = CreateMessage(
            png,
            BridgeToken,
            RequestId,
            1,
            1,
            "image/jpeg");
        string wrongDimensions = CreateMessage(
            png,
            BridgeToken,
            RequestId,
            2,
            1);
        string extra = CreateMessage(
            png,
            BridgeToken,
            RequestId,
            1,
            1,
            extra: true);

        Assert.IsFalse(_parser.TryParse(stale, expected, out _));
        Assert.IsFalse(_parser.TryParse(wrongMime, expected, out _));
        Assert.IsFalse(_parser.TryParse(
            wrongDimensions,
            expected,
            out _));
        Assert.IsFalse(_parser.TryParse(extra, expected, out _));
    }

    [TestMethod]
    public void PngMessage_RejectsBadBase64TruncatedOrMismatchedPng()
    {
        PendingPreviewPngCopy expected = CreatePending(
            new PreviewPngSize(1, 1));
        byte[] wrongSignature = CreateStructurallyValidPng(1, 1);
        wrongSignature[0] = 0;
        byte[] wrongHeaderSize = CreateStructurallyValidPng(2, 1);

        Assert.IsFalse(_parser.TryParse(
            CreateMessage(
                wrongSignature,
                BridgeToken,
                RequestId,
                1,
                1),
            expected,
            out _));
        Assert.IsFalse(_parser.TryParse(
            CreateMessage(
                wrongHeaderSize,
                BridgeToken,
                RequestId,
                1,
                1),
            expected,
            out _));
        Assert.IsFalse(_parser.TryParse(
            """
            {"type":"png","token":"00112233445566778899AABBCCDDEEFF",
             "requestId":"FFEEDDCCBBAA99887766554433221100",
             "mimeType":"image/png","width":1,"height":1,"payload":"!!!!"}
            """,
            expected,
            out _));
    }

    private static PendingPreviewPngCopy CreatePending(
        PreviewPngSize size)
    {
        return new PendingPreviewPngCopy(
            BridgeToken,
            RequestId,
            new PreviewPngCopyPlan(
                size,
                PreviewPngSourceState.CurrentValid));
    }

    private static string CreateMessage(
        byte[] png,
        string token,
        string requestId,
        int width,
        int height,
        string mimeType = "image/png",
        bool extra = false)
    {
        Dictionary<string, object> message = new()
        {
            ["type"] = "png",
            ["token"] = token,
            ["requestId"] = requestId,
            ["mimeType"] = mimeType,
            ["width"] = width,
            ["height"] = height,
            ["payload"] = Convert.ToBase64String(png)
        };
        if (extra)
        {
            message["url"] = "https://example.test";
        }

        return JsonSerializer.Serialize(message);
    }

    private static byte[] CreateStructurallyValidPng(
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
