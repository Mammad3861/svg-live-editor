using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InspectorSourceGuardTests
{
    private readonly InspectorSourceGuard _guard = new();

    [TestMethod]
    public void CurrentIndex_CanSynchronizeOnlyOutsideActiveTextComposition()
    {
        Assert.IsTrue(_guard.CanUseIndex(
            isIndexCurrent: true,
            indexRevision: 8,
            sourceRevision: 8,
            isEditorTextCompositionActive: false));
        Assert.IsFalse(_guard.CanUseIndex(
            isIndexCurrent: true,
            indexRevision: 8,
            sourceRevision: 8,
            isEditorTextCompositionActive: true));
    }

    [TestMethod]
    public void StaleOrInvalidIndex_CannotChangeEditorSelectionOrSource()
    {
        Assert.IsFalse(_guard.CanUseIndex(
            isIndexCurrent: false,
            indexRevision: 8,
            sourceRevision: 8,
            isEditorTextCompositionActive: false));
        Assert.IsFalse(_guard.CanUseIndex(
            isIndexCurrent: true,
            indexRevision: 7,
            sourceRevision: 8,
            isEditorTextCompositionActive: false));
    }
}
