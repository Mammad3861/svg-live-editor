using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewZoomBridgeTests
{
    private readonly PreviewZoomBridge _bridge = new();

    [TestMethod]
    public void CtrlWheelTransition_LeavesFitAndUpdatesArtworkDimensions()
    {
        PreviewZoomRequest request = new(
            PreviewZoomDirection.In,
            ContentX: 0.5,
            ContentY: 0.5,
            AnchorX: 300,
            AnchorY: 200,
            ViewportWidth: 600,
            ViewportHeight: 400);

        PreviewZoomTransition transition = _bridge.Apply(
            PreviewZoomState.Fit,
            new SvgCanvasSize(1000, 500),
            fitScale: 0.6,
            request);

        Assert.AreEqual(PreviewZoomMode.Manual, transition.State.Mode);
        Assert.AreEqual(0.75, transition.State.ManualScale, 0.0001);
        Assert.AreEqual(750, transition.RenderedWidth, 0.0001);
        Assert.AreEqual(375, transition.RenderedHeight, 0.0001);
        Assert.AreEqual("75%", transition.State.DisplayText);
    }
}
