using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SourceRevisionTrackerTests
{
    [TestMethod]
    public void Advance_InvalidatesEveryPreviouslyCapturedSourceRevision()
    {
        SourceRevisionTracker tracker = new();
        long initial = tracker.Current;
        long firstEdit = tracker.Advance();
        long secondEdit = tracker.Advance();

        Assert.IsFalse(tracker.IsCurrent(initial));
        Assert.IsFalse(tracker.IsCurrent(firstEdit));
        Assert.IsTrue(tracker.IsCurrent(secondEdit));
    }
}
