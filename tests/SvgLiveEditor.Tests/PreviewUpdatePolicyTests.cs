using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewUpdatePolicyTests
{
    private readonly PreviewUpdatePolicy _policy = new();

    [TestMethod]
    public void ZoomUpdate_NeverNavigatesOrShowsTheFullLoadingState()
    {
        PreviewUpdateDecision withPreview = _policy.Decide(
            PreviewUpdateKind.Zoom,
            hasVisiblePreview: true);
        PreviewUpdateDecision withoutPreview = _policy.Decide(
            PreviewUpdateKind.Zoom,
            hasVisiblePreview: false);

        Assert.IsFalse(withPreview.RequiresNavigation);
        Assert.IsFalse(withPreview.ShowsFullLoadingState);
        Assert.IsFalse(withoutPreview.RequiresNavigation);
        Assert.IsFalse(withoutPreview.ShowsFullLoadingState);
    }

    [TestMethod]
    public void SourceUpdate_NavigatesButKeepsAnExistingPreviewVisible()
    {
        PreviewUpdateDecision withPreview = _policy.Decide(
            PreviewUpdateKind.Source,
            hasVisiblePreview: true);
        PreviewUpdateDecision withoutPreview = _policy.Decide(
            PreviewUpdateKind.Source,
            hasVisiblePreview: false);

        Assert.IsTrue(withPreview.RequiresNavigation);
        Assert.IsFalse(withPreview.ShowsFullLoadingState);
        Assert.IsTrue(withoutPreview.RequiresNavigation);
        Assert.IsTrue(withoutPreview.ShowsFullLoadingState);
    }
}
