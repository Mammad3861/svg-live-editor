using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewRenderReadinessTests
{
    [TestMethod]
    public void NavigationAndImageMustBothCompleteBeforeReady()
    {
        PreviewRenderReadiness readiness = new();
        readiness.Begin(renderRevision: 4, sourceRevision: 12);

        Assert.AreEqual(
            PreviewRenderReadinessResult.Waiting,
            readiness.RecordNavigation(4, isSuccess: true));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ready,
            readiness.RecordImage(4, Loaded(sourceRevision: 12)));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ignored,
            readiness.RecordImage(4, Loaded(sourceRevision: 12)));
    }

    [TestMethod]
    public void ImageMayLoadBeforeNavigationCompletionWithoutPrematureReady()
    {
        PreviewRenderReadiness readiness = new();
        readiness.Begin(renderRevision: 5, sourceRevision: 13);

        Assert.AreEqual(
            PreviewRenderReadinessResult.Waiting,
            readiness.RecordImage(5, Loaded(sourceRevision: 13)));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ready,
            readiness.RecordNavigation(5, isSuccess: true));
    }

    [TestMethod]
    public void StaleRenderAndSourceCompletionsCannotReadyTheCurrentImage()
    {
        PreviewRenderReadiness readiness = new();
        readiness.Begin(renderRevision: 6, sourceRevision: 20);

        Assert.AreEqual(
            PreviewRenderReadinessResult.Ignored,
            readiness.RecordNavigation(5, isSuccess: true));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ignored,
            readiness.RecordImage(6, Loaded(sourceRevision: 19)));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Waiting,
            readiness.RecordNavigation(6, isSuccess: true));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ready,
            readiness.RecordImage(6, Loaded(sourceRevision: 20)));
    }

    [TestMethod]
    public void NavigationImageErrorAndTimeoutFailOnlyTheCurrentRender()
    {
        PreviewRenderReadiness navigationFailure = new();
        navigationFailure.Begin(7, 21);
        Assert.AreEqual(
            PreviewRenderReadinessResult.Error,
            navigationFailure.RecordNavigation(7, isSuccess: false));

        PreviewRenderReadiness imageFailure = new();
        imageFailure.Begin(8, 22);
        Assert.AreEqual(
            PreviewRenderReadinessResult.Waiting,
            imageFailure.RecordImage(
                8,
                new PreviewImageLoadMessage(
                    PreviewImageLoadState.Error,
                    22,
                    0,
                    0)));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Error,
            imageFailure.RecordNavigation(8, isSuccess: true));

        PreviewRenderReadiness timeout = new();
        timeout.Begin(9, 23);
        Assert.AreEqual(
            PreviewRenderReadinessResult.Ignored,
            timeout.Timeout(8));
        Assert.AreEqual(
            PreviewRenderReadinessResult.Error,
            timeout.Timeout(9));
    }

    private static PreviewImageLoadMessage Loaded(long sourceRevision) =>
        new(
            PreviewImageLoadState.Loaded,
            sourceRevision,
            NaturalWidth: 300,
            NaturalHeight: 150);
}
