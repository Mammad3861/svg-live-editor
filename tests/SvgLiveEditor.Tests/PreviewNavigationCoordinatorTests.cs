using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewNavigationCoordinatorTests
{
    private const string FileA = "<svg><rect fill=\"red\"/><text>FILE A</text></svg>";
    private const string FileB = "<svg><circle fill=\"blue\"/><text>FILE B</text></svg>";
    private static readonly SvgCanvasSize CanvasSize = new(300, 150);
    private static readonly SvgVisualDocument VisualDocument = new(
        new SvgVisualViewport(
            0,
            0,
            300,
            150,
            SvgPreserveAspectRatio.Default),
        []);

    [TestMethod]
    public void RapidReplacement_RendersOnlyTheLatestPendingContentAfterActiveNavigation()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest first = Enqueue(coordinator, 1, FileA);
        Assert.AreEqual(first, coordinator.TryBeginNext());

        Enqueue(coordinator, 2, FileA);
        PreviewRenderRequest latest = Enqueue(coordinator, 3, FileB);

        Assert.IsTrue(coordinator.TryComplete(
            first.Revision,
            isSuccess: true,
            out bool firstWasLatest));
        Assert.IsFalse(firstWasLatest);
        Assert.AreEqual(latest, coordinator.TryBeginNext());
        Assert.AreEqual(FileB, latest.Svg);
        Assert.AreEqual(3, latest.SourceRevision);
        Assert.IsTrue(coordinator.TryComplete(
            latest.Revision,
            isSuccess: true,
            out bool latestWasLatest));
        Assert.IsTrue(latestWasLatest);
    }

    [TestMethod]
    public void StaleCompletion_CannotCompleteOrSupersedeTheActiveRevision()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest active = Enqueue(coordinator, 10, FileA);
        coordinator.TryBeginNext();
        PreviewRenderRequest latest = Enqueue(
            coordinator,
            11,
            FileB,
            PreviewZoomState.At100Percent,
            new PreviewViewportPosition(0.75, 0.25));

        Assert.IsFalse(coordinator.TryComplete(
            active.Revision + 100,
            isSuccess: true,
            out _));
        Assert.IsNull(coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(
            active.Revision,
            isSuccess: true,
            out bool activeWasLatest));
        Assert.IsFalse(activeWasLatest);
        Assert.AreEqual(latest, coordinator.TryBeginNext());
    }

    [TestMethod]
    public void IdenticalSourceRevision_IsIdempotentUntilForcedRefresh()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest first = Enqueue(coordinator, 20, FileA);
        Assert.AreEqual(first, coordinator.TryBeginNext());

        Assert.IsFalse(coordinator.TryEnqueue(
            20,
            FileA,
            CanvasSize,
            VisualDocument,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center,
            force: false,
            out _));
        Assert.IsTrue(coordinator.TryComplete(
            first.Revision,
            isSuccess: true,
            out bool wasLatest));
        Assert.IsTrue(wasLatest);
        Assert.IsFalse(coordinator.TryEnqueue(
            20,
            FileA,
            CanvasSize,
            VisualDocument,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center,
            force: false,
            out _));

        Assert.IsTrue(coordinator.TryEnqueue(
            20,
            FileA,
            CanvasSize,
            VisualDocument,
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center,
            force: true,
            out PreviewRenderRequest? refresh));
        Assert.IsNotNull(refresh);
        Assert.AreNotEqual(first.Revision, refresh.Revision);
    }

    [TestMethod]
    public void FailedLatestRender_DoesNotSuppressAValidRetry()
    {
        PreviewNavigationCoordinator coordinator = new();
        PreviewRenderRequest failed = Enqueue(coordinator, 30, FileB);
        Assert.AreEqual(failed, coordinator.TryBeginNext());
        Assert.IsTrue(coordinator.TryComplete(
            failed.Revision,
            isSuccess: false,
            out bool wasLatest));
        Assert.IsTrue(wasLatest);

        PreviewRenderRequest retry = Enqueue(coordinator, 30, FileB);
        Assert.AreNotEqual(failed.Revision, retry.Revision);
    }

    private static PreviewRenderRequest Enqueue(
        PreviewNavigationCoordinator coordinator,
        long sourceRevision,
        string svg,
        PreviewZoomState? zoom = null,
        PreviewViewportPosition? viewport = null)
    {
        Assert.IsTrue(coordinator.TryEnqueue(
            sourceRevision,
            svg,
            CanvasSize,
            VisualDocument,
            zoom ?? PreviewZoomState.Fit,
            viewport ?? PreviewViewportPosition.Center,
            force: false,
            out PreviewRenderRequest? request));
        Assert.IsNotNull(request);
        return request;
    }
}
