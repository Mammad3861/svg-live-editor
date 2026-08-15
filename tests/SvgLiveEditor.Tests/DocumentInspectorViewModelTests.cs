using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class DocumentInspectorViewModelTests
{
    private readonly SvgDocumentIndexService _indexService = new();

    [TestMethod]
    public void Load_PreservesUniqueIdSelectionAcrossStructuralChanges()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><rect id=\"hero\"/></g></svg>";
        const string after =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><defs/><g><rect id=\"hero\"/></g></svg>";
        DocumentInspectorViewModel inspector = new();
        SvgDocumentIndex beforeIndex = _indexService.Build(before).Document!;
        inspector.Load(beforeIndex, preferredSelection: null);
        inspector.SelectNode(beforeIndex.Elements.Single(element => element.Id == "hero"));
        SvgElementIdentity identity = inspector.CaptureSelectionIdentity()!;

        inspector.Load(_indexService.Build(after).Document!, identity);

        Assert.IsTrue(inspector.HasSelection);
        Assert.AreEqual("hero", inspector.SelectedElement!.Element.Id);
        Assert.AreEqual("0/1/0", inspector.SelectedElement.Element.StructuralPath);
    }

    [TestMethod]
    public void Load_ClearsSelectionWhenSelectedElementNoLongerExists()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"gone\"/></svg>";
        const string after =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle/></svg>";
        DocumentInspectorViewModel inspector = new();
        SvgDocumentIndex beforeIndex = _indexService.Build(before).Document!;
        inspector.Load(beforeIndex, preferredSelection: null);
        inspector.SelectNode(beforeIndex.Elements.Single(element => element.Id == "gone"));

        inspector.Load(
            _indexService.Build(after).Document!,
            inspector.CaptureSelectionIdentity());

        Assert.IsFalse(inspector.HasSelection);
        Assert.IsNull(inspector.SelectedElement);
        Assert.AreEqual(0, inspector.Properties.Count);
    }

    [TestMethod]
    public void SelectNode_ProvidesSupportedPropertiesAndReadOnlyPathData()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path id=\"route\" d=\"M0 0L10 10\"/></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);

        inspector.SelectNode(index.Elements.Single(element => element.Name == "path"));

        CollectionAssert.AreEqual(
            new[] { "id", "fill", "stroke", "stroke-width", "d" },
            inspector.Properties.Select(property => property.Name).ToArray());
        Assert.IsNotNull(inspector.Opacity);
        Assert.IsTrue(inspector.Opacity.IsEnabled);
        Assert.AreEqual(100, inspector.Opacity.Percent);
        SvgPropertyViewModel pathData = inspector.Properties.Single(property => property.Name == "d");
        Assert.IsTrue(pathData.IsReadOnly);
        Assert.AreEqual("M0 0L10 10", pathData.Value);
    }

    [TestMethod]
    public void SelectionShowsReadOnlyLayerPositionAndParentContext()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"cards\"><rect id=\"one\"/><circle id=\"two\"/><text id=\"three\">T</text></g></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);

        inspector.SelectNode(index.Elements.Single(element =>
            element.Id == "two"));

        Assert.IsTrue(inspector.HasLayerPosition);
        Assert.AreEqual(
            "Layer 2 of 3 · front to back",
            inspector.LayerPosition!.DisplayText);
        Assert.AreEqual("g #cards", inspector.LayerPosition.ParentLabel);
        StringAssert.Contains(
            inspector.LayerPosition.BoundaryExplanation,
            "cannot cross group");
    }

    [TestMethod]
    public void LayerAndStructureSelectionsStaySynchronizedAndRevealGroups()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"outer\"><g id=\"inner\"><rect id=\"shape\"/></g></g></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);
        SvgElementNode shape = index.Elements.Single(element =>
            element.Id == "shape");

        inspector.SelectNode(
            shape,
            InspectorSelectionOrigin.PreviewNavigation);

        Assert.AreEqual("shape", inspector.SelectedElement!.Element.Id);
        Assert.AreEqual("shape", inspector.SelectedLayer!.Element.Id);
        Assert.IsTrue(inspector.SelectedLayer.IsSelected);
        Assert.IsTrue(inspector.SelectedLayer.Parent!.IsExpanded);
        Assert.IsTrue(inspector.SelectedLayer.Parent.Parent!.IsExpanded);

        SvgLayerViewModel outer = inspector.LayerRoots.Single();
        inspector.AcceptLayerSelection(outer);
        Assert.AreEqual("outer", inspector.SelectedElement!.Element.Id);
        Assert.AreEqual("outer", inspector.SelectedLayer!.Element.Id);
    }

    [TestMethod]
    public void Load_PreservesUnrelatedLayerAndStructureExpansionAcrossAuthoringEdit()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"left\"><rect/></g><g id=\"right\"><circle/></g></svg>";
        const string after =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"left\"><rect/><line id=\"new\"/></g><g id=\"right\"><circle/></g></svg>";
        DocumentInspectorViewModel inspector = new();
        SvgDocumentIndex beforeIndex = _indexService.Build(before).Document!;
        inspector.Load(beforeIndex, preferredSelection: null, source: before);
        SvgLayerViewModel rightLayer = inspector.LayerRoots.Single(layer =>
            layer.Element.Id == "right");
        SvgElementViewModel rightStructure = inspector.Roots.Single()
            .Children.Single(element => element.Element.Id == "right");
        rightLayer.IsExpanded = false;
        rightStructure.IsExpanded = false;

        inspector.Load(
            _indexService.Build(after).Document!,
            new SvgElementIdentity("line", "new", "0/0/1"),
            source: after);

        Assert.IsFalse(inspector.LayerRoots.Single(layer =>
            layer.Element.Id == "right").IsExpanded);
        Assert.IsFalse(inspector.Roots.Single().Children.Single(element =>
            element.Element.Id == "right").IsExpanded);
        Assert.AreEqual("new", inspector.SelectedElement!.Element.Id);
        Assert.AreEqual("new", inspector.SelectedLayer!.Element.Id);
    }

    [TestMethod]
    public void StructureTspanSelectionRevealsItsNearestTextLayer()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\"><tspan>سلام</tspan></text></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);

        inspector.SelectNode(index.Elements.Single(element =>
            element.Name == "tspan"));

        Assert.AreEqual("tspan", inspector.SelectedElement!.Element.Name);
        Assert.AreEqual("label", inspector.SelectedLayer!.Element.Id);
    }

    [TestMethod]
    public void InvalidSourceClearsBothLayersAndStructure()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>";
        DocumentInspectorViewModel inspector = new();
        inspector.Load(
            _indexService.Build(source).Document!,
            preferredSelection: null,
            source: source);

        inspector.ShowUnavailable("Current source is invalid.");

        Assert.AreEqual(0, inspector.Roots.Count);
        Assert.AreEqual(0, inspector.LayerRoots.Count);
        Assert.IsNull(inspector.SelectedLayer);
    }

    [TestMethod]
    public void LockedParentMakesDescendantPropertiesAndOpacityReadOnly()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"><rect id=\"shape\" x=\"1\"/></g></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);
        SvgLayerViewModel group = inspector.LayerRoots.Single();

        Assert.IsTrue(inspector.ToggleLayerLock(group));
        inspector.SelectNode(index.Elements.Single(element =>
            element.Id == "shape"));

        Assert.IsTrue(inspector.Properties.All(property =>
            property.IsReadOnly));
        Assert.IsNotNull(inspector.Opacity);
        Assert.IsFalse(inspector.Opacity.IsEnabled);
        StringAssert.Contains(
            inspector.Opacity.UnavailableReason,
            "Unlock");
    }

    [TestMethod]
    public void LayerIconAutomationNamesDescribeActionAndInheritedLockState()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"><rect id=\"shape\"/></g></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null, source: source);
        SvgLayerViewModel group = inspector.LayerRoots.Single();

        Assert.AreEqual("Hide g #group", group.VisibilityAutomationName);
        Assert.AreEqual("Lock g #group", group.LockAutomationName);
        Assert.IsTrue(inspector.ToggleLayerLock(group));

        SvgLayerViewModel lockedGroup = inspector.LayerRoots.Single();
        SvgLayerViewModel inheritedChild = lockedGroup.Children.Single();
        Assert.AreEqual("Unlock g #group", lockedGroup.LockAutomationName);
        Assert.AreEqual(
            "rect #shape locked by a parent group",
            inheritedChild.LockAutomationName);
        Assert.IsFalse(inheritedChild.CanToggleLock);
    }

    [TestMethod]
    public void TextAndTspanExposeConstrainedBidiPresentationProperties()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text><tspan>سلام!</tspan></text></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null);

        foreach (string elementName in new[] { "text", "tspan" })
        {
            inspector.SelectNode(index.Elements.Single(element =>
                element.Name == elementName));

            foreach (string propertyName in
                     new[] { "direction", "unicode-bidi", "text-anchor" })
            {
                SvgPropertyViewModel property =
                    inspector.Properties.Single(item =>
                        item.Name == propertyName);
                Assert.IsTrue(property.HasAllowedValues);
                CollectionAssert.Contains(
                    property.AllowedValues.ToArray(),
                    string.Empty);
            }
        }

        SvgPropertyViewModel direction =
            inspector.Properties.Single(property =>
                property.Name == "direction");
        CollectionAssert.AreEqual(
            new[] { "", "ltr", "rtl" },
            direction.AllowedValues.ToArray());
    }

    [TestMethod]
    public void TextExposesEditableFontSuggestionsAndTypographyProperties()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"1\" y=\"20\" font-family=\"Segoe UI, sans-serif\" font-size=\"18\">Text</text></svg>";
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.SetFontFamilySuggestions(
            ["Segoe UI", "Arial", "Tahoma", "sans-serif"]);
        inspector.Load(index, preferredSelection: null);
        inspector.SelectNode(index.Elements.Single(element =>
            element.Name == "text"));

        SvgPropertyViewModel family = inspector.Properties.Single(property =>
            property.Name == "font-family");
        Assert.IsTrue(family.HasSuggestedValues);
        Assert.IsFalse(family.HasAllowedValues);
        Assert.AreEqual("Segoe UI", family.Value);
        Assert.AreEqual(
            "Segoe UI, sans-serif",
            family.SerializedValue);
        CollectionAssert.Contains(
            family.SuggestedValues.ToArray(),
            "Tahoma");
        foreach (string propertyName in
                 new[] { "font-size", "font-weight", "font-style" })
        {
            Assert.IsTrue(inspector.Properties.Any(property =>
                property.Name == propertyName));
        }
    }

    [TestMethod]
    public void ShowUnavailable_ClearsTreeSelectionAndProperties()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>";
        DocumentInspectorViewModel inspector = new();
        inspector.Load(_indexService.Build(source).Document!, preferredSelection: null);

        inspector.ShowUnavailable("Current source is invalid.");

        Assert.IsFalse(inspector.HasIndex);
        Assert.IsFalse(inspector.HasSelection);
        Assert.AreEqual(0, inspector.Roots.Count);
        Assert.AreEqual(0, inspector.Properties.Count);
        StringAssert.Contains(inspector.StateMessage, "invalid");
    }

    [TestMethod]
    public void SelectionAdvisoryIsNonDestructiveAndClearsWithSelection()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text direction=\"ltr\">بهروز</text><rect/></svg>";
        string sourceSnapshot = source;
        SvgDocumentIndex index = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(index, preferredSelection: null);
        inspector.SelectNode(index.Elements.Single(element =>
            element.Name == "text"));

        inspector.SetSelectionAdvisory(
            SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection);

        Assert.AreEqual(
            SvgTextDirectionAdvisoryService.RtlTextWithLtrDirection,
            inspector.SelectionAdvisory);
        inspector.SelectNode(index.Elements.Single(element =>
            element.Name == "rect"));
        Assert.AreEqual(string.Empty, inspector.SelectionAdvisory);
        Assert.IsTrue(source.Equals(
            sourceSnapshot,
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void StructureRangeUsesOnlyVisibleSiblingsAndDuplicateIdsStayDistinct()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"one\"><rect id=\"same\"/><rect id=\"same\"/></g><g id=\"two\"><circle/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);
        SvgElementViewModel group = inspector.Roots.Single().Children[0];
        SvgElementViewModel first = group.Children[0];
        SvgElementViewModel second = group.Children[1];
        SvgElementViewModel otherContainerChild =
            inspector.Roots.Single().Children[1].Children.Single();

        inspector.Roots.Single().IsExpanded = false;
        Assert.AreEqual(0, inspector.GetVisibleStructureSiblings(first).Count);
        inspector.Roots.Single().IsExpanded = true;
        group.IsExpanded = false;
        Assert.AreEqual(0, inspector.GetVisibleStructureSiblings(first).Count);

        group.IsExpanded = true;
        IReadOnlyList<SvgElementIdentity> visible =
            inspector.GetVisibleStructureSiblings(first);
        CollectionAssert.AreEqual(
            new[] { first.Element.Identity, second.Element.Identity },
            visible.ToArray());
        Assert.AreNotEqual(
            first.Element.Identity.StructuralPath,
            second.Element.Identity.StructuralPath);

        SvgMultiSelectionService selectionService = new();
        SvgMultiSelectionState anchor = selectionService.Replace(
            3,
            first.Element.Identity);
        SvgMultiSelectionChange toggled = selectionService.Toggle(
            anchor,
            3,
            second.Element.Identity);
        Assert.IsTrue(toggled.IsSuccess);
        CollectionAssert.AreEqual(
            visible.ToArray(),
            toggled.State.Identities.ToArray());
        SvgMultiSelectionChange range = selectionService.SelectRange(
            anchor,
            3,
            visible,
            second.Element.Identity);
        Assert.IsTrue(range.IsSuccess);
        SvgMultiSelectionChange crossParent = selectionService.SelectRange(
            anchor,
            3,
            inspector.GetVisibleStructureSiblings(otherContainerChild),
            otherContainerChild.Element.Identity);
        Assert.IsFalse(crossParent.IsSuccess);
        StringAssert.Contains(crossParent.ErrorMessage!, "different layer container");
    }

    [TestMethod]
    public void StructureAndLayersExposeTheSamePrimaryAndSecondarySelection()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"first\"/><text id=\"second\">سلام</text></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        DocumentInspectorViewModel inspector = new();
        inspector.Load(document, preferredSelection: null, source: source);
        SvgElementNode first = document.Elements.Single(element =>
            element.Id == "first");
        SvgElementNode second = document.Elements.Single(element =>
            element.Id == "second");

        inspector.SelectNode(second, InspectorSelectionOrigin.ExplicitTreeNavigation);
        inspector.SetMultiSelectionPresentation(
            new HashSet<SvgElementIdentity>
            {
                first.Identity,
                second.Identity
            },
            second.Identity);

        SvgElementViewModel primary = inspector.FindViewModel(second)!;
        SvgElementViewModel secondary = inspector.FindViewModel(first)!;
        Assert.IsTrue(primary.IsSelected);
        Assert.IsFalse(primary.IsMultiSelected);
        StringAssert.Contains(primary.AutomationName, "primary selected");
        Assert.IsTrue(secondary.IsMultiSelected);
        StringAssert.Contains(secondary.AutomationName, "selected");
        Assert.IsTrue(inspector.FindLayerViewModel(first)!.IsMultiSelected);
        Assert.IsTrue(inspector.FindLayerViewModel(second)!.IsSelected);
        Assert.AreEqual(2, inspector.MultiSelectionCount);
        StringAssert.Contains(source, "سلام");
    }
}
