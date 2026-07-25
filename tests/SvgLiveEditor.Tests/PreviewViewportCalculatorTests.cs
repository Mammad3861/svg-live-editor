using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewViewportCalculatorTests
{
    private readonly PreviewViewportCalculator _calculator = new();

    [TestMethod]
    public void CaptureAndRestore_PreservesTheVisibleCenterAcrossSourceRefresh()
    {
        PreviewViewportPosition captured = _calculator.Capture(
            new PreviewScrollPosition(800, 300),
            contentWidth: 2000,
            contentHeight: 1000,
            viewportWidth: 600,
            viewportHeight: 400);

        PreviewScrollPosition restored = _calculator.Restore(
            captured,
            contentWidth: 3000,
            contentHeight: 1500,
            viewportWidth: 600,
            viewportHeight: 400);

        Assert.AreEqual(0.55, captured.CenterX, 0.0001);
        Assert.AreEqual(0.5, captured.CenterY, 0.0001);
        Assert.AreEqual(1350, restored.Left, 0.0001);
        Assert.AreEqual(550, restored.Top, 0.0001);
        Assert.IsTrue(restored.Left > 0);
        Assert.IsTrue(restored.Top > 0);
    }

    [TestMethod]
    public void Restore_ClampsNormalizedAndNonFinitePositions()
    {
        PreviewScrollPosition clamped = _calculator.Restore(
            new PreviewViewportPosition(2, -1),
            contentWidth: 1200,
            contentHeight: 800,
            viewportWidth: 600,
            viewportHeight: 400);
        PreviewScrollPosition safeDefault = _calculator.Restore(
            new PreviewViewportPosition(double.NaN, double.PositiveInfinity),
            contentWidth: 1200,
            contentHeight: 800,
            viewportWidth: 600,
            viewportHeight: 400);

        Assert.AreEqual(600, clamped.Left, 0.0001);
        Assert.AreEqual(0, clamped.Top, 0.0001);
        Assert.AreEqual(0, safeDefault.Left, 0.0001);
        Assert.AreEqual(0, safeDefault.Top, 0.0001);
    }
}
