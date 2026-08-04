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
}
