using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewZoomCalculatorTests
{
    private readonly PreviewZoomCalculator _calculator = new();

    [TestMethod]
    public void CalculateFitScale_KeepsCompleteCanvasInsidePaddedViewport()
    {
        SvgCanvasSize canvas = new(1040, 440);

        double scale = _calculator.CalculateFitScale(canvas, 620, 632);

        Assert.AreEqual(0.5480769, scale, 0.0001);
        Assert.IsTrue((canvas.Width * scale) <= 620 - 48);
        Assert.IsTrue((canvas.Height * scale) <= 632 - 48);
    }

    [TestMethod]
    public void FitScale_RecalculatesWhenViewportChanges()
    {
        SvgCanvasSize canvas = new(1000, 500);

        double wide = _calculator.CalculateFitScale(canvas, 1050, 550);
        double narrow = _calculator.CalculateFitScale(canvas, 550, 550);

        Assert.AreEqual(1.0, wide, 0.0001);
        Assert.AreEqual(0.5, narrow, 0.0001);
    }

    [TestMethod]
    public void FitScale_UsesWebViewCssPixelsAtScaledWindowsDpi()
    {
        SvgCanvasSize canvas = new(1040, 440);

        double scale = _calculator.CalculateFitScale(
            canvas,
            viewportWidth: 620,
            viewportHeight: 632,
            dpiScaleX: 1.25,
            dpiScaleY: 1.25);

        Assert.AreEqual(0.4288461, scale, 0.0001);
    }

    [TestMethod]
    public void ZoomFromFit_LeavesFitAndMovesToVisibleStep()
    {
        PreviewZoomState zoomedIn = _calculator.ZoomIn(PreviewZoomState.Fit, fitScale: 0.55);
        PreviewZoomState zoomedOut = _calculator.ZoomOut(PreviewZoomState.Fit, fitScale: 0.55);

        Assert.AreEqual(PreviewZoomMode.Manual, zoomedIn.Mode);
        Assert.AreEqual(0.75, zoomedIn.ManualScale, 0.0001);
        Assert.AreEqual("75%", zoomedIn.DisplayText);
        Assert.AreEqual(PreviewZoomMode.Manual, zoomedOut.Mode);
        Assert.AreEqual(0.5, zoomedOut.ManualScale, 0.0001);
    }

    [TestMethod]
    public void ManualZoom_IsIndependentOfViewportFitScale()
    {
        PreviewZoomState manual = new(PreviewZoomMode.Manual, 1.25);

        Assert.AreEqual(1.25, _calculator.ResolveScale(manual, fitScale: 0.4), 0.0001);
        Assert.AreEqual(1.25, _calculator.ResolveScale(manual, fitScale: 1.8), 0.0001);
    }

    [TestMethod]
    public void Reset_IsExactly100PercentAndLimitsAreEnforced()
    {
        PreviewZoomState reset = _calculator.Reset();
        PreviewZoomState minimum = reset;
        PreviewZoomState maximum = reset;

        for (int index = 0; index < 30; index++)
        {
            minimum = _calculator.ZoomOut(minimum, fitScale: 1.0);
            maximum = _calculator.ZoomIn(maximum, fitScale: 1.0);
        }

        Assert.AreEqual(1.0, reset.ManualScale, 0.0001);
        Assert.AreEqual("100%", reset.DisplayText);
        Assert.AreEqual(PreviewZoomCalculator.MinimumScale, minimum.ManualScale, 0.0001);
        Assert.AreEqual(PreviewZoomCalculator.MaximumScale, maximum.ManualScale, 0.0001);
    }
}
