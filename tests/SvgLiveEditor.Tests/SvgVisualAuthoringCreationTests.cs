using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgVisualAuthoringCreationTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgElementCreationService _service = new();

    [TestMethod]
    [DataRow(SvgCreateElementKind.Rectangle, "rect", "width")]
    [DataRow(SvgCreateElementKind.Circle, "circle", "r")]
    [DataRow(SvgCreateElementKind.Ellipse, "ellipse", "rx")]
    [DataRow(SvgCreateElementKind.Line, "line", "x2")]
    [DataRow(SvgCreateElementKind.Text, "text", "font-family")]
    [DataRow(SvgCreateElementKind.Group, "g", "id")]
    public void Create_EachKnownKindUsesBoundedDefaultsAndBecomesFrontmost(
        SvgCreateElementKind kind,
        string expectedName,
        string expectedAttribute)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"100 200 400 200\"><rect id=\"existing\"/></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            selection: null,
            kind,
            new SvgCanvasSize(400, 200));

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;
        SvgElementNode created = rebuilt.FindBestMatch(
            result.PreferredSelection!)!;
        Assert.AreEqual(expectedName, created.Name);
        Assert.IsNotNull(created.FindAttribute(expectedAttribute));
        Assert.AreSame(rebuilt.Roots.Single().Children.Last(), created);
        Assert.IsTrue(created.Attributes
            .Where(attribute => attribute.Name is not "fill"
                and not "stroke"
                and not "font-family"
                and not "text-anchor"
                and not "id")
            .Select(attribute => attribute.RawValue)
            .All(value => !value.Contains("NaN", StringComparison.Ordinal)
                && !value.Contains("Infinity", StringComparison.Ordinal)));
        CollectionAssert.IsSubsetOf(
            created.Attributes.Select(attribute => attribute.Name).ToArray(),
            SvgPropertySchema.GetProperties(created.Name)
                .Select(property => property.Name)
                .ToArray());
    }

    [TestMethod]
    public void Create_SelectedGroupReceivesFrontmostChildAndPreservesCrlf()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <g id=\"target\">\r\n    <circle id=\"old\"/>\r\n  </g>\r\n</svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "target"),
            SvgCreateElementKind.Rectangle,
            new SvgCanvasSize(300, 150));
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;
        SvgElementNode target = Find(rebuilt, "target");

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual("rect", target.Children.Last().Name);
        StringAssert.Contains(candidate, "\r\n    <rect");
        Assert.AreEqual(0, candidate.Replace("\r\n", string.Empty).Count(c => c == '\n'));
    }

    [TestMethod]
    public void Create_SelectedArtworkUsesItsParentAndDefinitionSelectionFallsBackToRoot()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><defs><path id=\"definition\"/></defs><g id=\"group\"><rect id=\"selected\"/></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult sibling = _service.CreateEdit(
            source,
            document,
            Find(document, "selected"),
            SvgCreateElementKind.Circle,
            new SvgCanvasSize(300, 150));
        SvgDocumentIndex siblingDocument = _indexService.Build(
            sibling.Edit!.Apply(source)).Document!;
        Assert.AreEqual(
            "circle",
            Find(siblingDocument, "group").Children.Last().Name);

        SvgAuthoringEditResult root = _service.CreateEdit(
            source,
            document,
            Find(document, "definition"),
            SvgCreateElementKind.Line,
            new SvgCanvasSize(300, 150));
        SvgDocumentIndex rootDocument = _indexService.Build(
            root.Edit!.Apply(source)).Document!;
        Assert.AreEqual("line", rootDocument.Roots.Single().Children.Last().Name);
    }

    [TestMethod]
    public void Create_ExpandsSelfClosingGroupWithoutSerializingOtherSource()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><!-- keep سلام --><g id=\"target\" /></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            Find(document, "target"),
            SvgCreateElementKind.Text,
            new SvgCanvasSize(300, 150));
        string candidate = result.Edit!.Apply(source);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        StringAssert.Contains(candidate, "<!-- keep سلام -->");
        StringAssert.Contains(candidate, "<g id=\"target\" ><text");
        StringAssert.Contains(candidate, "</text></g>");
    }

    [TestMethod]
    public void Create_EmptyGroupAppearsSelectedInLayersStructureAndProperties()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            null,
            SvgCreateElementKind.Group,
            new SvgCanvasSize(300, 150));
        string candidate = result.Edit!.Apply(source);
        SvgDocumentIndex rebuilt = _indexService.Build(candidate).Document!;
        DocumentInspectorViewModel inspector = new();

        inspector.Load(
            rebuilt,
            result.PreferredSelection,
            source: candidate);

        Assert.AreEqual("g", inspector.LayerRoots.Single().Element.Name);
        Assert.AreEqual(
            "g",
            inspector.Roots.Single().Children.Single().Element.Name);
        Assert.AreEqual("g", inspector.SelectedLayer!.Element.Name);
        Assert.AreEqual("g", inspector.SelectedElement!.Element.Name);
        CollectionAssert.IsSubsetOf(
            inspector.Properties.Select(property => property.Name).ToArray(),
            SvgPropertySchema.GetProperties("g")
                .Select(property => property.Name)
                .ToArray());
    }

    [TestMethod]
    public void Create_RejectsLockedParentAndStaleSpanWithoutAnEdit()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"locked\"></g></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgElementNode group = Find(document, "locked");

        SvgAuthoringEditResult locked = _service.CreateEdit(
            source,
            document,
            group,
            SvgCreateElementKind.Rectangle,
            new SvgCanvasSize(300, 150),
            element => ReferenceEquals(element, group));
        SvgAuthoringEditResult stale = _service.CreateEdit(
            source.Replace("<g", "<g data-x=\"1\"", StringComparison.Ordinal),
            document,
            group,
            SvgCreateElementKind.Rectangle,
            new SvgCanvasSize(300, 150));

        Assert.IsFalse(locked.IsSuccess);
        Assert.IsNull(locked.Edit);
        StringAssert.Contains(locked.ErrorMessage, "Unlock");
        Assert.IsFalse(stale.IsSuccess);
        Assert.IsNull(stale.Edit);
    }

    [TestMethod]
    public void Create_RejectsUnknownEnumValueWithoutConstructingMarkup()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;

        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            null,
            (SvgCreateElementKind)999,
            new SvgCanvasSize(300, 150));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage, "not supported");
    }

    [TestMethod]
    public void Create_IsExactlyOneUndoAndRedoOperation()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";
        SvgDocumentIndex document = _indexService.Build(source).Document!;
        SvgAuthoringEditResult result = _service.CreateEdit(
            source,
            document,
            null,
            SvgCreateElementKind.Group,
            new SvgCanvasSize(300, 150));
        TextDocument textDocument = new(source);
        textDocument.UndoStack.MarkAsOriginalFile();

        new AvalonEditDocumentEditService().Apply(textDocument, result.Edit!);
        string created = textDocument.Text;
        textDocument.UndoStack.Undo();
        Assert.AreEqual(source, textDocument.Text);
        Assert.IsFalse(textDocument.UndoStack.CanUndo);
        textDocument.UndoStack.Redo();
        Assert.AreEqual(created, textDocument.Text);
    }

    private static SvgElementNode Find(SvgDocumentIndex document, string id) =>
        document.Elements.Single(element => element.Id == id);
}
