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
}
