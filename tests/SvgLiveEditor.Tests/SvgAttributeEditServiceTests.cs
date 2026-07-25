using System.Text;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgAttributeEditServiceTests
{
    private readonly SvgDocumentIndexService _indexService = new();
    private readonly SvgAttributeEditService _editService = new();

    [TestMethod]
    public void CreateEdit_ReplacesOnlyExistingAttributeValue()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <!-- keep this comment -->
              <rect id="box" fill = '#fff' stroke="#111">سلام</rect>
            </svg>
            """;
        SvgElementNode rectangle = FindElement(source, "rect");

        SvgAttributeEditResult result = _editService.CreateEdit(
            source,
            rectangle,
            "fill",
            "#0ea5e9");

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        const string expected = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <!-- keep this comment -->
              <rect id="box" fill = '#0ea5e9' stroke="#111">سلام</rect>
            </svg>
            """;
        Assert.AreEqual(expected, result.Edit!.Apply(source));
        Assert.AreEqual(
            rectangle.FindAttribute("fill")!.ValueSpan.Start,
            result.Edit.Start);
        Assert.AreEqual(4, result.Edit.Length);
    }

    [TestMethod]
    public void CreateEdit_AddsOnlyMissingAttributeAtEndOfStartTag()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <rect x=\"1\" />\r\n</svg>";
        SvgElementNode rectangle = FindElement(source, "rect");

        SvgAttributeEditResult result = _editService.CreateEdit(
            source,
            rectangle,
            "fill",
            "#fff");

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <rect x=\"1\" fill=\"#fff\"/>\r\n</svg>",
            result.Edit!.Apply(source));
        Assert.AreEqual(0, result.Edit.Length);
    }

    [TestMethod]
    public void CreateEdit_AddsPersianIdWithoutChangingPersianText()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"4\">سلام SVG</text></svg>";
        SvgElementNode text = FindElement(source, "text");

        SvgAttributeEditResult result = _editService.CreateEdit(
            source,
            text,
            "id",
            "عنوان");

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text x=\"4\" id=\"عنوان\">سلام SVG</text></svg>",
            result.Edit!.Apply(source));
    }

    [TestMethod]
    [DataRow("opacity", "1.2")]
    [DataRow("width", "-1")]
    [DataRow("fill", "url(https://example.test/paint.svg)")]
    public void CreateEdit_InvalidValueReturnsNoEditAndLeavesSourceUnchanged(
        string attribute,
        string value)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" /></svg>";
        SvgElementNode rectangle = FindElement(source, "rect");

        SvgAttributeEditResult result = _editService.CreateEdit(
            source,
            rectangle,
            attribute,
            value);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [TestMethod]
    public void CreateEdit_PathDataIsReadOnly()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0L10 10\" /></svg>";
        SvgElementNode path = FindElement(source, "path");

        SvgAttributeEditResult result = _editService.CreateEdit(
            source,
            path,
            "d",
            "M1 1");

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage, "read-only");
    }

    [TestMethod]
    public void CreateEdit_RejectsAnElementSpanFromAnOlderSourceRevision()
    {
        const string original =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text fill=\"red\">سلام</text></svg>";
        SvgElementNode text = FindElement(original, "text");
        const string current =
            "<!-- user typed before validation completed -->\n<svg xmlns=\"http://www.w3.org/2000/svg\"><text fill=\"red\">سلام</text></svg>";

        SvgAttributeEditResult result = _editService.CreateEdit(
            current,
            text,
            "fill",
            "blue");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        StringAssert.Contains(result.ErrorMessage, "source changed");
        StringAssert.Contains(current, "سلام");
    }

    [TestMethod]
    public void CreateEdit_AllowsInternalPaintReferenceAndRejectsExternalResource()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs><linearGradient id="safe"><stop offset="0" /></linearGradient></defs>
              <rect />
            </svg>
            """;
        SvgElementNode rectangle = FindElement(source, "rect");

        SvgAttributeEditResult safe = _editService.CreateEdit(
            source,
            rectangle,
            "fill",
            "url(#safe)");
        SvgAttributeEditResult unsafeResult = _editService.CreateEdit(
            source,
            rectangle,
            "fill",
            "url(data:image/svg+xml;base64,AAAA)");

        Assert.IsTrue(safe.IsSuccess, safe.ErrorMessage);
        Assert.IsTrue(new SvgValidationService().Validate(safe.Edit!.Apply(source)).IsValid);
        Assert.IsFalse(unsafeResult.IsSuccess);
        Assert.IsNull(unsafeResult.Edit);
    }

    [TestMethod]
    public void PropertyEdit_RoundTripsAsExactUtf8WithoutBom()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <text x=\"4\">سلام SVG</text>\r\n</svg>";
        SvgElementNode text = FindElement(source, "text");
        SvgAttributeEditResult edit = _editService.CreateEdit(
            source,
            text,
            "y",
            "24");
        string expected = edit.Edit!.Apply(source);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Tests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "property-edit.svg");

        try
        {
            Directory.CreateDirectory(directory);
            Utf8FileService fileService = new();
            fileService.WriteAllText(path, expected);

            Assert.AreEqual(expected, fileService.ReadAllText(path));
            Assert.IsFalse(
                File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private SvgElementNode FindElement(string source, string name)
    {
        SvgDocumentIndexResult index = _indexService.Build(source);
        Assert.IsTrue(index.IsIndexed, index.IndexError);
        return index.Document!.Elements.Single(element =>
            element.Name.Equals(name, StringComparison.Ordinal));
    }
}
