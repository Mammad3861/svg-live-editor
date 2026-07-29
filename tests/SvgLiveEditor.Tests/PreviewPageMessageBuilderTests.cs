using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewPageMessageBuilderTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private readonly PreviewPageMessageBuilder _builder = new();

    [TestMethod]
    public void ZoomStateMessage_UsesOnlyTheFixedTokenBoundFiniteSchema()
    {
        string json = _builder.BuildZoomStateMessage(
            BridgeToken,
            renderedWidth: 1250,
            renderedHeight: 625,
            new PreviewViewportPosition(0.75, 0.25));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.AreEqual(6, root.EnumerateObject().Count());
        Assert.AreEqual("zoomState", root.GetProperty("type").GetString());
        Assert.AreEqual(BridgeToken, root.GetProperty("token").GetString());
        Assert.AreEqual(1250, root.GetProperty("renderedWidth").GetDouble());
        Assert.AreEqual(625, root.GetProperty("renderedHeight").GetDouble());
        Assert.AreEqual(0.75, root.GetProperty("centerX").GetDouble());
        Assert.AreEqual(0.25, root.GetProperty("centerY").GetDouble());
    }

    [TestMethod]
    public void ZoomStateMessage_RejectsInvalidTokensDimensionsAndViewportValues()
    {
        Assert.Throws<ArgumentException>(() =>
            _builder.BuildZoomStateMessage(
                "not-a-token",
                100,
                100,
                PreviewViewportPosition.Center));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildZoomStateMessage(
                BridgeToken,
                double.NaN,
                100,
                PreviewViewportPosition.Center));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildZoomStateMessage(
                BridgeToken,
                100,
                PreviewPageMessageBuilder.MaximumRenderedDimension + 1,
                PreviewViewportPosition.Center));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildZoomStateMessage(
                BridgeToken,
                100,
                100,
                new PreviewViewportPosition(1.1, 0.5)));
    }

    [TestMethod]
    public void PanStateMessage_UsesOnlyTokenBooleanAndSystemThresholdSchema()
    {
        string json = _builder.BuildPanStateMessage(
            BridgeToken,
            enabled: true,
            minimumHorizontalDragDistance: 4,
            minimumVerticalDragDistance: 5);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.AreEqual(5, root.EnumerateObject().Count());
        Assert.AreEqual("panState", root.GetProperty("type").GetString());
        Assert.AreEqual(BridgeToken, root.GetProperty("token").GetString());
        Assert.IsTrue(root.GetProperty("enabled").GetBoolean());
        Assert.AreEqual(
            4,
            root.GetProperty("minimumHorizontalDragDistance").GetDouble());
        Assert.AreEqual(
            5,
            root.GetProperty("minimumVerticalDragDistance").GetDouble());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildPanStateMessage(
                BridgeToken,
                enabled: false,
                minimumHorizontalDragDistance: 0,
                minimumVerticalDragDistance: 4));
    }

    [TestMethod]
    public void PngRequestMessage_UsesOnlyValidatedBoundedFields()
    {
        const string requestId = "FFEEDDCCBBAA99887766554433221100";
        string json = _builder.BuildPngRequestMessage(
            BridgeToken,
            requestId,
            new PreviewPngSize(1040, 440));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.AreEqual(5, root.EnumerateObject().Count());
        Assert.AreEqual("copyPng", root.GetProperty("type").GetString());
        Assert.AreEqual(BridgeToken, root.GetProperty("token").GetString());
        Assert.AreEqual(requestId, root.GetProperty("requestId").GetString());
        Assert.AreEqual(1040, root.GetProperty("width").GetInt32());
        Assert.AreEqual(440, root.GetProperty("height").GetInt32());

        Assert.Throws<ArgumentException>(() =>
            _builder.BuildPngRequestMessage(
                BridgeToken,
                "bad",
                new PreviewPngSize(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildPngRequestMessage(
                BridgeToken,
                requestId,
                new PreviewPngSize(4096, 4096)));
    }

    [TestMethod]
    public void HorizontalScrollMessage_UsesOnlyCurrentTokenAndBoundedDelta()
    {
        string json = _builder.BuildHorizontalScrollMessage(
            BridgeToken,
            deltaX: -12.5);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.AreEqual(3, root.EnumerateObject().Count());
        Assert.AreEqual(
            "horizontalScroll",
            root.GetProperty("type").GetString());
        Assert.AreEqual(
            BridgeToken,
            root.GetProperty("token").GetString());
        Assert.AreEqual(
            -12.5,
            root.GetProperty("deltaX").GetDouble(),
            0.0001);

        Assert.Throws<ArgumentException>(() =>
            _builder.BuildHorizontalScrollMessage("stale", 1));
        foreach (double invalid in new[]
                 {
                     0,
                     double.NaN,
                     double.PositiveInfinity,
                     PreviewPageMessageBuilder.MaximumHorizontalScrollDelta + 1
                 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _builder.BuildHorizontalScrollMessage(
                    BridgeToken,
                    invalid));
        }
    }
}
