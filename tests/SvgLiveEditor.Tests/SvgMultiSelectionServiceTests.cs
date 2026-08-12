using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgMultiSelectionServiceTests
{
    private readonly SvgMultiSelectionService _service = new();

    [TestMethod]
    public void ToggleAddsRemovesAndMaintainsPrimaryAndAnchor()
    {
        SvgElementIdentity first = Identity("rect", "a", "/svg[1]/rect[1]");
        SvgElementIdentity second = Identity("circle", "b", "/svg[1]/circle[1]");
        SvgMultiSelectionState state = _service.Replace(4, first);

        SvgMultiSelectionChange added = _service.Toggle(state, 4, second);
        Assert.IsTrue(added.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { first, second },
            added.State.Identities.ToArray());
        Assert.AreEqual(second, added.State.Primary);
        Assert.AreEqual(second, added.State.Anchor);

        SvgMultiSelectionChange removed = _service.Toggle(
            added.State,
            4,
            second);
        Assert.IsTrue(removed.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { first },
            removed.State.Identities.ToArray());
        Assert.AreEqual(first, removed.State.Primary);
        Assert.AreEqual(first, removed.State.Anchor);
    }

    [TestMethod]
    public void ToggleRejectsStaleRevisionAndMaximumOverflow()
    {
        SvgElementIdentity first = Identity("rect", null, "/svg[1]/rect[1]");
        SvgMultiSelectionState state = _service.Replace(3, first);
        Assert.IsFalse(_service.Toggle(state, 4, first).IsSuccess);

        SvgElementIdentity[] maximum = Enumerable.Range(
                0,
                SvgMultiSelectionService.MaximumSelectionCount)
            .Select(index => Identity(
                "rect",
                null,
                $"/svg[1]/rect[{index + 1}]"))
            .ToArray();
        state = new SvgMultiSelectionState(
            3,
            maximum,
            maximum[0],
            maximum[0]);
        SvgMultiSelectionChange overflow = _service.Toggle(
            state,
            3,
            Identity("circle", null, "/svg[1]/circle[1]"));
        Assert.IsFalse(overflow.IsSuccess);
        Assert.AreEqual(maximum.Length, overflow.State.Identities.Count);
    }

    [TestMethod]
    public void DuplicateAuthoredIdsRemainDistinctThroughStructuralIdentity()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\"/><rect id=\"same\"/></svg>";
        SvgDocumentIndex document =
            new SvgDocumentIndexService().Build(source).Document!;
        SvgElementNode[] rectangles = document.Elements
            .Where(element => element.Name == "rect")
            .ToArray();

        SvgMultiSelectionState state = _service.Replace(
            1,
            rectangles[0].Identity);
        SvgMultiSelectionChange change = _service.Toggle(
            state,
            1,
            rectangles[1].Identity);

        Assert.IsTrue(change.IsSuccess);
        Assert.AreEqual(2, change.State.Identities.Count);
        Assert.AreNotEqual(
            change.State.Identities[0].StructuralPath,
            change.State.Identities[1].StructuralPath);
    }

    [TestMethod]
    public void RangeIsBoundedToTheSuppliedVisibleSiblingContainer()
    {
        SvgElementIdentity first = Identity("rect", null, "/svg[1]/g[1]/rect[1]");
        SvgElementIdentity second = Identity("rect", null, "/svg[1]/g[1]/rect[2]");
        SvgElementIdentity nested = Identity("rect", null, "/svg[1]/g[2]/rect[1]");
        SvgMultiSelectionState state = _service.Replace(8, first);

        SvgMultiSelectionChange range = _service.SelectRange(
            state,
            8,
            [first, second],
            second);
        Assert.IsTrue(range.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { first, second },
            range.State.Identities.ToArray());

        SvgMultiSelectionChange crossedContainer = _service.SelectRange(
            state,
            8,
            [nested],
            nested);
        Assert.IsFalse(crossedContainer.IsSuccess);
    }

    [TestMethod]
    public void ReconcileDropsMissingItemsAndKeepsAValidPrimary()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"kept\"/><circle/></svg>";
        const string after =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"kept\"/></svg>";
        SvgDocumentIndex oldDocument =
            new SvgDocumentIndexService().Build(before).Document!;
        SvgElementNode[] oldItems = oldDocument.Elements
            .Where(element => element.Name is "rect" or "circle")
            .ToArray();
        SvgMultiSelectionState state = new(
            1,
            oldItems.Select(item => item.Identity).ToArray(),
            oldItems[1].Identity,
            oldItems[0].Identity);

        SvgMultiSelectionState reconciled = _service.Reconcile(
            state,
            2,
            new SvgDocumentIndexService().Build(after).Document!);

        Assert.AreEqual(1, reconciled.Identities.Count);
        Assert.AreEqual("kept", reconciled.Primary?.Id);
        Assert.AreEqual("kept", reconciled.Anchor?.Id);
    }

    [TestMethod]
    public void ReconcileClearsMalformedOverMaximumState()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>";
        SvgElementIdentity[] malformed = Enumerable.Range(0, 129)
            .Select(index => Identity(
                "rect",
                null,
                $"/svg[1]/rect[{index + 1}]"))
            .ToArray();
        SvgMultiSelectionState state = new(
            1,
            malformed,
            malformed[0],
            malformed[0]);

        SvgMultiSelectionState reconciled = _service.Reconcile(
            state,
            2,
            new SvgDocumentIndexService().Build(source).Document!);

        Assert.AreEqual(0, reconciled.Identities.Count);
        Assert.IsNull(reconciled.Primary);
    }

    private static SvgElementIdentity Identity(
        string name,
        string? id,
        string path) => new(name, id, path);
}
