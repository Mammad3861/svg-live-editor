using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewRenderTransportPolicyTests
{
    private static readonly SvgCanvasSize Canvas = new(300, 150);
    private static readonly SvgVisualViewport Viewport = new(
        0,
        0,
        300,
        150,
        SvgPreserveAspectRatio.Default);
    private readonly PreviewRenderTransportPolicy _policy = new();

    [TestMethod]
    public void CurrentTrustedPage_UpdatesImageWithoutReplacingTheDocument()
    {
        PreviewRenderRequest previous = CreateRequest(1, 1, Canvas, Viewport);
        PreviewRenderRequest current = CreateRequest(2, 2, Canvas, Viewport);

        Assert.AreEqual(
            PreviewRenderTransport.InPlaceImage,
            _policy.Decide(
                current,
                previous,
                hasVisiblePreview: true,
                hasTrustedPageToken: true));
    }

    [TestMethod]
    public void MissingOrChangedPageContract_RequiresNavigation()
    {
        PreviewRenderRequest previous = CreateRequest(1, 1, Canvas, Viewport);
        PreviewRenderRequest current = CreateRequest(2, 2, Canvas, Viewport);

        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(current, null, true, true));
        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(current, previous, false, true));
        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(current, previous, true, false));
        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(
                current with { RequiresNavigation = true },
                previous,
                true,
                true));
        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(
                current with { CanvasSize = new SvgCanvasSize(640, 480) },
                previous,
                true,
                true));
        Assert.AreEqual(
            PreviewRenderTransport.Navigation,
            _policy.Decide(
                current with
                {
                    VisualDocument = new SvgVisualDocument(
                        Viewport with { Width = 640 },
                        [])
                },
                previous,
                true,
                true));
    }

    private static PreviewRenderRequest CreateRequest(
        long renderRevision,
        long sourceRevision,
        SvgCanvasSize canvas,
        SvgVisualViewport viewport) =>
        new(
            renderRevision,
            sourceRevision,
            $"<svg data-revision=\"{sourceRevision}\"/>",
            canvas,
            new SvgVisualDocument(viewport, []),
            PreviewZoomState.Fit,
            PreviewViewportPosition.Center,
            RequiresNavigation: false);
}
