using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgLayerWorkspaceServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();

    [TestMethod]
    public void BuildShowsTopmostFirstAndKeepsNestedGroups()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="back"/>
              <defs><path id="definition"/></defs>
              <g id="group"><circle id="inside-back"/><text>سلام دنیا</text></g>
              <path id="front" d="M0 0L1 1"/>
            </svg>
            """;
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerWorkspace workspace =
            new SvgLayerWorkspaceService().Build(document, source);

        CollectionAssert.AreEqual(
            new[] { "front", "group", "back" },
            workspace.Roots.Select(item => item.Element.Id).ToArray());
        SvgLayerItem group = workspace.Roots.Single(item => item.IsGroup);
        CollectionAssert.AreEqual(
            new[] { "text", "circle" },
            group.Children.Select(item => item.Element.Name).ToArray());
        StringAssert.Contains(group.Children[0].Label, "سلام دنیا");
        Assert.IsFalse(workspace.ItemsByPath.Values.Any(item =>
            item.Element.Id == "definition"));
    }

    [TestMethod]
    public void OpaqueIdentitiesSurviveEditsReorderAndDuplicateIds()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\" x=\"1\"/><circle id=\"same\" cx=\"2\"/></svg>";
        const string edited =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\" x=\"3\"/><circle id=\"same\" cx=\"2\"/></svg>";
        const string reordered =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle id=\"same\" cx=\"2\"/><rect id=\"same\" x=\"3\"/></svg>";
        SvgLayerWorkspaceService service = new();
        SvgLayerWorkspace first = service.Build(
            _indexService.Build(before).Document!,
            before);
        string rectId = first.ItemsByPath.Values.Single(item =>
            item.Element.Name == "rect").OpaqueId;
        string circleId = first.ItemsByPath.Values.Single(item =>
            item.Element.Name == "circle").OpaqueId;

        SvgLayerWorkspace second = service.Build(
            _indexService.Build(edited).Document!,
            edited);
        SvgLayerWorkspace third = service.Build(
            _indexService.Build(reordered).Document!,
            reordered);

        Assert.AreEqual(rectId, second.ItemsByPath.Values.Single(item =>
            item.Element.Name == "rect").OpaqueId);
        Assert.AreEqual(rectId, third.ItemsByPath.Values.Single(item =>
            item.Element.Name == "rect").OpaqueId);
        Assert.AreEqual(circleId, third.ItemsByPath.Values.Single(item =>
            item.Element.Name == "circle").OpaqueId);
        Assert.AreNotEqual(rectId, circleId);
    }

    [TestMethod]
    public void GroupLockPropagatesAndResetsWithDocumentSession()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"locked\"><rect id=\"child\"/></g><circle id=\"peer\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerWorkspaceService service = new();
        SvgLayerWorkspace workspace = service.Build(document, source);
        SvgLayerItem group = workspace.ItemsByPath.Values.Single(item =>
            item.Element.Id == "locked");

        Assert.IsTrue(service.ToggleLock(group.OpaqueId));
        SvgLayerWorkspace locked = service.Build(document, source);
        SvgLayerItem child = locked.ItemsByPath.Values.Single(item =>
            item.Element.Id == "child");
        Assert.IsTrue(locked.ItemsByOpaqueId[group.OpaqueId].IsLocked);
        Assert.IsTrue(child.IsEffectivelyLocked);
        Assert.IsTrue(service.IsEffectivelyLocked(document, child.Element));

        service.BeginDocumentSession();
        SvgLayerWorkspace reset = service.Build(document, source);
        Assert.IsFalse(reset.ItemsByPath.Values.Any(item => item.IsLocked));
        Assert.IsFalse(reset.ItemsByPath.Values.Any(item =>
            item.IsEffectivelyLocked));
    }

    [TestMethod]
    public void EffectiveParentVisibilityIsShownOnChildren()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g display=\"none\"><rect id=\"child\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgLayerWorkspace workspace =
            new SvgLayerWorkspaceService().Build(document, source);
        SvgLayerItem child = workspace.ItemsByPath.Values.Single(item =>
            item.Element.Id == "child");

        Assert.IsFalse(child.Visibility.IsVisible);
        Assert.IsTrue(child.Visibility.IsHiddenByAncestor);
        Assert.IsFalse(child.Visibility.CanToggle);
        StringAssert.Contains(
            child.Visibility.UnavailableReason,
            "parent");
    }

    [TestMethod]
    public void VisibilityOwnershipSurvivesUndoRedoButNotDirectElementEdits()
    {
        const string visible =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\"/></svg>";
        const string hidden =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" display=\"none\"/></svg>";
        const string sourceEditedHidden =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" x=\"1\" display=\"none\"/></svg>";
        SvgLayerWorkspaceService service = new();
        SvgLayerItem initial = service.Build(
            _indexService.Build(visible).Document!,
            visible).Roots.Single();
        service.SetHiddenAttributeOwned(initial.OpaqueId, isOwned: true);

        SvgLayerItem afterHide = service.Build(
            _indexService.Build(hidden).Document!,
            hidden).Roots.Single();
        SvgLayerItem afterUndo = service.Build(
            _indexService.Build(visible).Document!,
            visible).Roots.Single();
        SvgLayerItem afterRedo = service.Build(
            _indexService.Build(hidden).Document!,
            hidden).Roots.Single();
        SvgLayerItem afterDirectEdit = service.Build(
            _indexService.Build(sourceEditedHidden).Document!,
            sourceEditedHidden).Roots.Single();

        Assert.IsTrue(afterHide.Visibility.CanToggle);
        Assert.IsTrue(afterUndo.Visibility.CanToggle);
        Assert.IsTrue(afterRedo.Visibility.CanToggle);
        Assert.IsFalse(afterDirectEdit.Visibility.CanToggle);
        StringAssert.Contains(
            afterDirectEdit.Visibility.UnavailableReason,
            "authored display");
    }
}
