using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class InspectorRefreshSelectionPolicyTests
{
    private const string OriginalSource =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/><text id=\"c\">text</text></svg>";
    private const string WithoutRectangle =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle id=\"b\"/><text id=\"c\">text</text></svg>";
    private const string EmptySource =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

    [TestMethod]
    public void RevisionBoundRestoreWinsAndPreservesIntentionalEmptySelection()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex document = BuildDocument(OriginalSource);
        SvgElementIdentity current = Find(document, "a");
        SvgElementIdentity restored = Find(document, "b");
        SvgElementIdentity preferred = Find(document, "c");

        SvgElementIdentity? restoredResult = coordinator.ResolveRefreshSelection(
            preferred,
            selectionRestoreApplied: true,
            restoredPrimary: restored,
            currentSelection: current,
            reconciledPrimary: current,
            document,
            out bool selectRootAfterRestore);
        SvgElementIdentity? emptyResult = coordinator.ResolveRefreshSelection(
            preferredSelection: null,
            selectionRestoreApplied: true,
            restoredPrimary: null,
            currentSelection: current,
            reconciledPrimary: current,
            document,
            out bool selectRootForEmptyRestore);

        Assert.AreEqual(restored, restoredResult);
        Assert.IsFalse(selectRootAfterRestore);
        Assert.IsNull(emptyResult);
        Assert.IsFalse(selectRootForEmptyRestore);
    }

    [TestMethod]
    public void OrdinaryRefreshUsesRootWhenEverySuppliedIdentityIsStale()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex original = BuildDocument(OriginalSource);
        SvgDocumentIndex current = BuildDocument(EmptySource);
        SvgElementIdentity staleCurrent = Find(original, "a");

        SvgElementIdentity? result = coordinator.ResolveRefreshSelection(
            preferredSelection: null,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: staleCurrent,
            reconciledPrimary: null,
            current,
            out bool selectRoot);

        Assert.IsNull(result);
        Assert.IsTrue(selectRoot);
    }

    [TestMethod]
    public void OrdinaryRefreshUsesRootWhenInvalidPreferredIdentityHasNoFallback()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex original = BuildDocument(OriginalSource);
        SvgDocumentIndex current = BuildDocument(EmptySource);
        SvgElementIdentity stalePreferred = Find(original, "c");

        SvgElementIdentity? result = coordinator.ResolveRefreshSelection(
            stalePreferred,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: null,
            reconciledPrimary: null,
            current,
            out bool selectRoot);

        Assert.IsNull(result);
        Assert.IsTrue(selectRoot);
    }

    [TestMethod]
    public void OrdinaryRefreshSkipsInvalidPreferredAndUsesValidLaterCandidate()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex original = BuildDocument(OriginalSource);
        SvgDocumentIndex current = BuildDocument(WithoutRectangle);
        SvgElementIdentity stalePreferred = Find(original, "a");
        SvgElementIdentity validCurrent = Find(current, "b");

        SvgElementIdentity? result = coordinator.ResolveRefreshSelection(
            stalePreferred,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: validCurrent,
            reconciledPrimary: null,
            current,
            out bool selectRoot);

        Assert.AreEqual(validCurrent, result);
        Assert.IsFalse(selectRoot);
    }

    [TestMethod]
    public void OrdinaryRefreshValidatesOldIdentityAndFallsBackToReconciledPrimary()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex original = BuildDocument(OriginalSource);
        SvgDocumentIndex current = BuildDocument(WithoutRectangle);
        SvgElementIdentity deletedPrimary = Find(original, "a");
        SvgElementIdentity survivingPrimary = Find(original, "b");

        SvgElementIdentity? result = coordinator.ResolveRefreshSelection(
            preferredSelection: null,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: deletedPrimary,
            reconciledPrimary: survivingPrimary,
            current,
            out bool selectRoot);

        Assert.AreEqual(Find(current, "b"), result);
        Assert.IsFalse(selectRoot);
    }

    [TestMethod]
    public void OrdinaryRefreshPrefersCurrentExplicitIdentityAndUsesRootOnlyWithoutSelection()
    {
        InspectorSelectionCoordinator coordinator = new();
        SvgDocumentIndex document = BuildDocument(OriginalSource);
        SvgElementIdentity current = Find(document, "a");
        SvgElementIdentity preferred = Find(document, "c");

        SvgElementIdentity? preferredResult = coordinator.ResolveRefreshSelection(
            preferred,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: current,
            reconciledPrimary: current,
            document,
            out bool selectRootAfterPreferred);
        SvgElementIdentity? ordinaryResult = coordinator.ResolveRefreshSelection(
            preferredSelection: null,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: current,
            reconciledPrimary: current,
            document,
            out bool selectRootAfterOrdinaryRefresh);
        SvgElementIdentity? rootResult = coordinator.ResolveRefreshSelection(
            preferredSelection: null,
            selectionRestoreApplied: false,
            restoredPrimary: null,
            currentSelection: null,
            reconciledPrimary: null,
            document,
            out bool selectRootWhenEmpty);

        Assert.AreEqual(preferred, preferredResult);
        Assert.IsFalse(selectRootAfterPreferred);
        Assert.AreEqual(current, ordinaryResult);
        Assert.IsFalse(selectRootAfterOrdinaryRefresh);
        Assert.IsNull(rootResult);
        Assert.IsTrue(selectRootWhenEmpty);
    }

    private static SvgDocumentIndex BuildDocument(string source) =>
        new SvgDocumentIndexService().Build(source).Document!;

    private static SvgElementIdentity Find(
        SvgDocumentIndex document,
        string id) =>
        document.Elements.Single(element => element.Id == id).Identity;
}
