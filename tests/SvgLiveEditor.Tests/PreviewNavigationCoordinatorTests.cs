using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewNavigationCoordinatorTests
{
    private const string FileA = "<svg><rect fill=\"red\"/><text>FILE A</text></svg>";
    private const string FileB = "<svg><circle fill=\"blue\"/><text>FILE B</text></svg>";
    private static readonly SvgCanvasSize CanvasSize = new(300, 150);

    [TestMethod]
    public void RapidReplacement_RendersOnlyTheLatestPendingContentAfterActiveNavigation()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest first = coordinator.Enqueue(
            FileA,
            CanvasSize,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center);
        Assert.AreEqual(first, coordinator.TryBeginNext());

        coordinator.Enqueue(
            FileA,
            CanvasSize,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center);
        PreviewRenderRequest latest = coordinator.Enqueue(
            FileB,
            CanvasSize,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center);

        Assert.IsTrue(coordinator.TryComplete(first.Revision, out bool firstWasLatest));
        Assert.IsFalse(firstWasLatest);
        Assert.AreEqual(latest, coordinator.TryBeginNext());
        Assert.AreEqual(FileB, latest.Svg);
        Assert.IsTrue(coordinator.TryComplete(latest.Revision, out bool latestWasLatest));
        Assert.IsTrue(latestWasLatest);
    }

    [TestMethod]
    public void StaleCompletion_CannotCompleteOrSupersedeTheActiveRevision()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest active = coordinator.Enqueue(
            FileA,
            CanvasSize,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center);
        coordinator.TryBeginNext();
        PreviewRenderRequest latest = coordinator.Enqueue(
            FileB,
            CanvasSize,
            PreviewZoomState.At100Percent,
            new PreviewViewportPosition(0.75, 0.25));

        Assert.IsFalse(coordinator.TryComplete(active.Revision + 100, out _));
        Assert.IsNull(coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(active.Revision, out bool activeWasLatest));
        Assert.IsFalse(activeWasLatest);
        Assert.AreEqual(latest, coordinator.TryBeginNext());
    }
}
