using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewDragGestureTests
{
    [TestMethod]
    public void ClickWithoutCrossingThreshold_DoesNotStartDrag()
    {
        PreviewDragGestureTracker tracker = new();
        tracker.Begin(10, 10);

        bool started = tracker.Move(
            12,
            12,
            isLeftButtonPressed: true,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4);
        tracker.Cancel();

        Assert.IsFalse(started);
        Assert.IsFalse(tracker.IsArmed);
    }

    [TestMethod]
    [DataRow(14, 10)]
    [DataRow(10, 14)]
    [DataRow(6, 10)]
    [DataRow(10, 6)]
    public void CrossingEitherWindowsThreshold_StartsOnce(
        double x,
        double y)
    {
        PreviewDragGestureTracker tracker = new();
        tracker.Begin(10, 10);

        Assert.IsTrue(tracker.Move(
            x,
            y,
            isLeftButtonPressed: true,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(tracker.IsArmed);
        Assert.IsFalse(tracker.Move(
            x + 10,
            y + 10,
            isLeftButtonPressed: true,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
    }

    [TestMethod]
    public void ButtonReleaseOrCancellation_DisarmsGesture()
    {
        PreviewDragGestureTracker tracker = new();
        tracker.Begin(0, 0);

        Assert.IsFalse(tracker.Move(
            20,
            20,
            isLeftButtonPressed: false,
            minimumHorizontalDistance: 4,
            minimumVerticalDistance: 4));
        Assert.IsFalse(tracker.IsArmed);

        tracker.Begin(0, 0);
        tracker.Cancel();
        Assert.IsFalse(tracker.IsArmed);
    }
}
