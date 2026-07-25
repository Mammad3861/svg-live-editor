using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgDocumentIndexServiceTests
{
    private readonly SvgDocumentIndexService _service = new();

    [TestMethod]
    public void Build_IndexesNestedElementsInDocumentOrder()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs><linearGradient id="paint"><stop offset="0" /></linearGradient></defs>
              <g id="layer">
                <rect id="box" />
                <circle />
              </g>
              <text>سلام</text>
            </svg>
            """;

        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsTrue(result.IsIndexed, result.IndexError);
        CollectionAssert.AreEqual(
            new[] { "svg", "defs", "linearGradient", "stop", "g", "rect", "circle", "text" },
            result.Document!.Elements.Select(element => element.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "defs", "g", "text" },
            result.Document.Roots[0].Children.Select(element => element.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "rect", "circle" },
            result.Document.Elements.Single(element => element.Id == "layer")
                .Children.Select(element => element.Name).ToArray());
    }

    [TestMethod]
    public void Build_HandlesSvgNamespacePrefixesAndNestedGroups()
    {
        const string source = """
            <s:svg xmlns:s="http://www.w3.org/2000/svg">
              <s:g id="outer"><s:g id="inner"><s:rect /></s:g></s:g>
            </s:svg>
            """;

        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsTrue(result.IsIndexed, result.IndexError);
        CollectionAssert.AreEqual(
            new[] { "svg", "g", "g", "rect" },
            result.Document!.Elements.Select(element => element.Name).ToArray());
        Assert.AreEqual("s:rect", result.Document.Elements[^1].QualifiedName);
        Assert.AreEqual("0/0/0/0", result.Document.Elements[^1].StructuralPath);
    }

    [TestMethod]
    public void Build_PreservesMissingAndDuplicateIdsWithoutGeneratingMetadata()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"same\"/><circle/><rect id=\"same\"/></svg>";

        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsTrue(result.IsIndexed, result.IndexError);
        SvgElementNode[] shapes = result.Document!.Elements.Skip(1).ToArray();
        Assert.AreEqual("rect #same", shapes[0].DisplayLabel);
        Assert.IsNull(shapes[1].Id);
        Assert.AreEqual("circle", shapes[1].DisplayLabel);
        Assert.AreEqual("rect #same", shapes[2].DisplayLabel);

        SvgElementNode? duplicateMatch = result.Document.FindBestMatch(
            new SvgElementIdentity("rect", "same", "0/2"));
        Assert.AreSame(shapes[2], duplicateMatch);
    }

    [TestMethod]
    public void Build_MapsStartTagsAttributeValuesAndPersianTextToExactSourceSpans()
    {
        const string source = """
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="http://www.w3.org/2000/svg">
              <!-- untouched -->
              <g id='گروه'>
                <text x="12" y="24">سلام SVG</text>
              </g>
            </svg>
            """;

        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsTrue(result.IsIndexed, result.IndexError);
        SvgElementNode group = result.Document!.Elements.Single(element => element.Name == "g");
        SvgElementNode text = result.Document.Elements.Single(element => element.Name == "text");
        Assert.AreEqual("<g id='گروه'>", Slice(source, group.StartTagSpan));
        Assert.AreEqual("گروه", Slice(source, group.FindAttribute("id")!.ValueSpan));
        Assert.AreEqual("<text x=\"12\" y=\"24\">", Slice(source, text.StartTagSpan));
        Assert.AreSame(
            text,
            result.Document.FindElementAtOffset(source.IndexOf("سلام", StringComparison.Ordinal)));
        Assert.AreSame(
            result.Document.Roots[0],
            result.Document.FindElementAtOffset(source.IndexOf("untouched", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FindElementAtOffset_ReturnsDeepestKnownElementAndMapsBackToStartTag()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><rect x=\"1\" /></g></svg>";
        SvgDocumentIndex document = _service.Build(source).Document!;
        int attributeOffset = source.IndexOf("x=\"1\"", StringComparison.Ordinal) + 2;

        SvgElementNode? selected = document.FindElementAtOffset(attributeOffset);

        Assert.IsNotNull(selected);
        Assert.AreEqual("rect", selected.Name);
        Assert.AreEqual("<rect x=\"1\" />", Slice(source, selected.StartTagSpan));
        Assert.AreSame(
            document.Roots[0],
            document.FindElementAtOffset(source.Length));
    }

    [TestMethod]
    public void FindBestMatch_PrefersUniqueIdThenStructuralPath()
    {
        const string before =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><rect id=\"hero\"/></g></svg>";
        const string after =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><defs/><g><rect id=\"hero\"/></g></svg>";
        SvgElementIdentity identity = _service.Build(before).Document!.Elements
            .Single(element => element.Id == "hero").Identity;

        SvgElementNode? match = _service.Build(after).Document!.FindBestMatch(identity);

        Assert.IsNotNull(match);
        Assert.AreEqual("hero", match.Id);
        Assert.AreEqual("0/1/0", match.StructuralPath);
    }

    [TestMethod]
    [DataRow("<svg xmlns=\"http://www.w3.org/2000/svg\"><g></svg>")]
    [DataRow("<root />")]
    public void Build_DoesNotIndexInvalidXmlOrNonSvgRoots(string source)
    {
        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsFalse(result.Validation.IsValid);
        Assert.IsFalse(result.IsIndexed);
        Assert.IsNull(result.Document);
    }

    [TestMethod]
    [DataRow("<!DOCTYPE svg><svg xmlns=\"http://www.w3.org/2000/svg\" />")]
    [DataRow("<svg xmlns=\"http://www.w3.org/2000/svg\"><script /></svg>")]
    [DataRow("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://example.test/a.png\" /></svg>")]
    public void Build_PreservesExistingSecurityRejections(string source)
    {
        SvgDocumentIndexResult result = _service.Build(source);

        Assert.IsFalse(result.Validation.IsValid);
        Assert.IsNull(result.Document);
    }

    private static string Slice(string source, SourceSpan span) =>
        source.Substring(span.Start, span.Length);
}
