using ICSharpCode.AvalonEdit.Document;
using System.Windows.Input;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgGroupServiceTests
{
    private readonly SvgGroupService _service = new();

    [TestMethod]
    public void ContiguousSiblingsAreWrappedWithoutChangingTheirBytesOrOrder()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/><line id=\"c\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode[] selected = document.Elements
            .Where(element => element.Id is "a" or "b")
            .ToArray();

        SvgAuthoringEditResult result = _service.CreateGroupEdit(
            source,
            document,
            selected);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        string candidate = result.Edit.Apply(source);
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><rect id=\"a\"/><circle id=\"b\"/></g><line id=\"c\"/></svg>",
            candidate);
        Assert.AreEqual("g", result.PreferredSelection?.Name);
        AssertSingleUndo(source, candidate, result.Edit);
    }

    [TestMethod]
    public void PreviewFocusedCtrlGRouteCreatesRealWrapperAndCtrlShiftGRemovesIt()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"b\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgLayoutShortcutAction groupAction = SvgLayoutShortcutRouter.Resolve(
            ModifierKeys.Control,
            Key.G,
            compositionSurfaceHasKeyboardFocus: true,
            editableControlHasKeyboardFocus: false,
            isTextCompositionActive: false);

        Assert.AreEqual(SvgLayoutShortcutAction.Group, groupAction);
        SvgAuthoringEditResult grouped = _service.CreateGroupEdit(
            source,
            document,
            document.Roots.Single().Children);
        Assert.IsTrue(grouped.IsSuccess, grouped.ErrorMessage);
        string groupedSource = grouped.Edit!.Apply(source);
        StringAssert.Contains(groupedSource, "<g><rect id=\"a\"/><circle id=\"b\"/></g>");
        AssertSingleUndo(source, groupedSource, grouped.Edit);

        SvgLayoutShortcutAction ungroupAction = SvgLayoutShortcutRouter.Resolve(
            ModifierKeys.Control | ModifierKeys.Shift,
            Key.G,
            compositionSurfaceHasKeyboardFocus: true,
            editableControlHasKeyboardFocus: false,
            isTextCompositionActive: false);
        Assert.AreEqual(SvgLayoutShortcutAction.Ungroup, ungroupAction);
        SvgDocumentIndex groupedDocument = Build(groupedSource);
        SvgAuthoringEditResult ungrouped = _service.CreateUngroupEdit(
            groupedSource,
            groupedDocument,
            groupedDocument.Elements.Single(element => element.Name == "g"));
        Assert.IsTrue(ungrouped.IsSuccess, ungrouped.ErrorMessage);
        Assert.AreEqual(source, ungrouped.Edit!.Apply(groupedSource));
        AssertSingleUndo(groupedSource, source, ungrouped.Edit);
    }

    [TestMethod]
    public void NonContiguousCrossParentStaleAndLockedSelectionsFailClosed()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"a\"/><circle id=\"middle\"/><line id=\"c\"/><g><rect id=\"nested\"/></g></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode a = ById(document, "a");
        SvgElementNode c = ById(document, "c");
        SvgElementNode nested = ById(document, "nested");

        Assert.IsFalse(_service.CreateGroupEdit(
            source,
            document,
            [a, c]).IsSuccess);
        Assert.IsFalse(_service.CreateGroupEdit(
            source,
            document,
            [a, nested]).IsSuccess);
        Assert.IsFalse(_service.CreateGroupEdit(
            source.Replace("id=\"a\"", "id=\"changed\"", StringComparison.Ordinal),
            document,
            [a, ById(document, "middle")]).IsSuccess);
        SvgAuthoringEditResult locked = _service.CreateGroupEdit(
            source,
            document,
            [a, ById(document, "middle")],
            element => ReferenceEquals(element, a));
        Assert.IsFalse(locked.IsSuccess);
        StringAssert.Contains(locked.ErrorMessage!, "Unlock");
    }

    [TestMethod]
    public void DuplicateAuthoredIdsDoNotMakeContiguousGroupingAmbiguous()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\"/><rect id=\"same\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode[] rectangles = document.Elements
            .Where(element => element.Name == "rect")
            .ToArray();

        SvgAuthoringEditResult result = _service.CreateGroupEdit(
            source,
            document,
            rectangles);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreNotEqual(
            rectangles[0].Identity.StructuralPath,
            rectangles[1].Identity.StructuralPath);
    }

    [TestMethod]
    public void InheritedSessionLockRejectsGroupingTheCompleteSelection()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"parent\"><rect/><circle/></g></svg>";
        SvgDocumentIndex document = Build(source);
        SvgLayerWorkspaceService workspaceService = new();
        SvgLayerWorkspace workspace = workspaceService.Build(document, source);
        SvgLayerItem parent = workspace.ItemsByPath.Values
            .Single(item => item.Element.Id == "parent");
        Assert.IsTrue(workspaceService.ToggleLock(parent.OpaqueId));
        workspaceService.Build(document, source);
        SvgElementNode group = document.Elements
            .Single(element => element.Id == "parent");

        SvgAuthoringEditResult result = _service.CreateGroupEdit(
            source,
            document,
            group.Children,
            element => workspaceService.IsEffectivelyLocked(
                document,
                element));

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "Unlock");
    }

    [TestMethod]
    public void NeutralGroupUngroupsInPlaceAndPreservesExactChildOrder()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><line id=\"before\"/><g><rect id=\"a\"/>\n<circle id=\"b\"/></g><line id=\"after\"/></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode group = document.Elements.Single(element => element.Name == "g");

        SvgAuthoringEditResult result = _service.CreateUngroupEdit(
            source,
            document,
            group);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Edit);
        string candidate = result.Edit.Apply(source);
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><line id=\"before\"/><rect id=\"a\"/>\n<circle id=\"b\"/><line id=\"after\"/></svg>",
            candidate);
        Assert.AreEqual("a", result.PreferredSelection?.Id);
        AssertSingleUndo(source, candidate, result.Edit);
    }

    [TestMethod]
    public void InheritedSessionLockRejectsUngroupWithoutAnEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"outer\"><g><rect/></g></g></svg>";
        SvgDocumentIndex document = Build(source);
        SvgLayerWorkspaceService workspaceService = new();
        SvgLayerWorkspace workspace = workspaceService.Build(document, source);
        SvgLayerItem outer = workspace.ItemsByPath.Values
            .Single(item => item.Element.Id == "outer");
        Assert.IsTrue(workspaceService.ToggleLock(outer.OpaqueId));
        workspaceService.Build(document, source);
        SvgElementNode inner = document.Elements
            .Single(element => element.Name == "g" && element.Id is null);

        SvgAuthoringEditResult result = _service.CreateUngroupEdit(
            source,
            document,
            inner,
            element => workspaceService.IsEffectivelyLocked(
                document,
                element));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage!, "Unlock");
    }

    [TestMethod]
    [DataRow("transform=\"translate(1)\"")]
    [DataRow("opacity=\"0.5\"")]
    [DataRow("fill=\"red\"")]
    [DataRow("style=\"stroke:black\"")]
    [DataRow("clip-path=\"url(#clip)\"")]
    [DataRow("data-name=\"Metadata still has discard semantics\"")]
    public void AttributedGroupIsRejectedRatherThanChangingSemantics(
        string attribute)
    {
        string source =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><g {attribute}><rect/></g></svg>";
        SvgDocumentIndex document = Build(source);
        SvgElementNode group = document.Elements.Single(element => element.Name == "g");

        SvgAuthoringEditResult result = _service.CreateUngroupEdit(
            source,
            document,
            group);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
    }

    private static SvgDocumentIndex Build(string source) =>
        new SvgDocumentIndexService().Build(source).Document!;

    private static SvgElementNode ById(
        SvgDocumentIndex document,
        string id) => document.Elements.Single(element => element.Id == id);

    private static void AssertSingleUndo(
        string source,
        string candidate,
        SourceTextEdit edit)
    {
        TextDocument document = new(source);
        new AvalonEditDocumentEditService().Apply(document, edit);
        Assert.AreEqual(candidate, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(candidate, document.Text);
    }
}
