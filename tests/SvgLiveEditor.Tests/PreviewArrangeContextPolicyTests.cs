using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewArrangeContextPolicyTests
{
    private const string SelectionId =
        "00112233445566778899AABBCCDDEEFF";

    [TestMethod]
    public void RequiresExactRevisionSelectionIdAndCurrentHostSelection()
    {
        PreviewContextMenuRequest request =
            new(10, 10, 100, 100, 7, SelectionId);

        Assert.IsTrue(PreviewArrangeContextPolicy.IsCurrent(
            request,
            visibleSourceRevision: 7,
            currentSourceRevision: 7,
            currentSelectionId: SelectionId,
            selectionIdentityMatches: true));
        Assert.IsFalse(PreviewArrangeContextPolicy.IsCurrent(
            request,
            visibleSourceRevision: 6,
            currentSourceRevision: 7,
            currentSelectionId: SelectionId,
            selectionIdentityMatches: true));
        Assert.IsFalse(PreviewArrangeContextPolicy.IsCurrent(
            request,
            visibleSourceRevision: 7,
            currentSourceRevision: 8,
            currentSelectionId: SelectionId,
            selectionIdentityMatches: true));
        Assert.IsFalse(PreviewArrangeContextPolicy.IsCurrent(
            request,
            visibleSourceRevision: 7,
            currentSourceRevision: 7,
            currentSelectionId: "FFEEDDCCBBAA99887766554433221100",
            selectionIdentityMatches: true));
        Assert.IsFalse(PreviewArrangeContextPolicy.IsCurrent(
            request,
            visibleSourceRevision: 7,
            currentSourceRevision: 7,
            currentSelectionId: SelectionId,
            selectionIdentityMatches: false));
    }
}
