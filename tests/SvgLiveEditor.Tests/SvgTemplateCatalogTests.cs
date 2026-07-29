using System.Xml.Linq;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgTemplateCatalogTests
{
    [TestMethod]
    public void Catalog_ContainsFiveOriginalSecureTemplates()
    {
        IReadOnlyList<SvgTemplateDefinition> templates =
            new SvgTemplateCatalog().LoadAll();
        SvgValidationService validator = new();

        Assert.HasCount(5, templates);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "blank",
                "app-icon",
                "social-card",
                "flow-diagram",
                "persian-rtl"
            },
            templates.Select(template => template.Id).ToArray());
        foreach (SvgTemplateDefinition template in templates)
        {
            SvgValidationResult validation =
                validator.Validate(template.Source);
            Assert.IsTrue(
                validation.IsValid,
                $"{template.Id}: {validation.Message}");
            StringAssert.Contains(
                template.Source,
                "xmlns=\"http://www.w3.org/2000/svg\"");
            Assert.IsFalse(
                template.Source.Contains(
                    "<script",
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                template.Source.Contains(
                    "foreignObject",
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                template.Source.Contains(
                    "http://",
                    StringComparison.OrdinalIgnoreCase)
                && !template.Source.Contains(
                    SvgValidationService.SvgNamespace,
                    StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void TemplateDimensions_MatchTheRequiredCanvasSizes()
    {
        IReadOnlyDictionary<string, SvgTemplateDefinition> templates =
            new SvgTemplateCatalog().LoadAll()
                .ToDictionary(template => template.Id);

        StringAssert.Contains(
            templates["blank"].Source,
            "viewBox=\"0 0 800 600\"");
        StringAssert.Contains(
            templates["app-icon"].Source,
            "viewBox=\"0 0 24 24\"");
        StringAssert.Contains(
            templates["social-card"].Source,
            "viewBox=\"0 0 1200 630\"");
        StringAssert.Contains(
            templates["flow-diagram"].Source,
            "marker-end=\"url(#arrow)\"");
    }

    [TestMethod]
    public void PersianTemplate_PreservesExactRtlText()
    {
        SvgTemplateDefinition template = new SvgTemplateCatalog()
            .LoadAll()
            .Single(item => item.Id == "persian-rtl");

        string[] expectedText =
        [
            "سلام، ویرایشگر زندهٔ SVG",
            "سلام! من بهروز هستم.",
            "این یک متن فارسی است!",
            "نسخه 2.0 آماده است.",
            "قیمت: ۱۲۳٬۴۵۶ تومان.",
            "SvgLiveEditor نسخه 0.5",
            "(سلام بهروز)",
            "Hello — سلام!"
        ];
        foreach (string value in expectedText)
        {
            StringAssert.Contains(template.Source, value);
        }

        Assert.IsFalse(template.Source.Any(character =>
            character is '\u200e' or '\u200f'
                or '\u2066' or '\u2067' or '\u2068' or '\u2069'));
    }

    [TestMethod]
    public void EveryTemplateTextElementHasExplicitSafeBidiSemantics()
    {
        foreach (SvgTemplateDefinition template in
                 new SvgTemplateCatalog().LoadAll())
        {
            XDocument document = XDocument.Parse(
                template.Source,
                LoadOptions.PreserveWhitespace);
            XElement[] textElements = document
                .Descendants(XName.Get(
                    "text",
                    SvgValidationService.SvgNamespace))
                .ToArray();

            foreach (XElement text in textElements)
            {
                string? direction = (string?)text.Attribute("direction");
                string? unicodeBidi = (string?)text.Attribute("unicode-bidi");
                string? textAnchor = (string?)text.Attribute("text-anchor");

                CollectionAssert.Contains(
                    new[] { "ltr", "rtl" },
                    direction,
                    $"{template.Id} text direction");
                CollectionAssert.Contains(
                    new[] { "embed", "plaintext" },
                    unicodeBidi,
                    $"{template.Id} unicode-bidi");
                CollectionAssert.Contains(
                    new[] { "start", "middle", "end" },
                    textAnchor,
                    $"{template.Id} text-anchor");

                if (template.Id == "persian-rtl")
                {
                    Assert.AreEqual("rtl", direction);
                    Assert.AreEqual("embed", unicodeBidi);
                    Assert.AreEqual("start", textAnchor);
                }
            }
        }
    }

    [TestMethod]
    public void MetadataIdentifiersAreStableUniqueAndDimensionsArePresent()
    {
        IReadOnlyList<SvgTemplateDefinition> templates =
            new SvgTemplateCatalog().LoadAll();

        Assert.HasCount(
            templates.Count,
            templates.Select(template => template.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        Assert.IsTrue(templates.All(template =>
            !string.IsNullOrWhiteSpace(template.Name)
            && !string.IsNullOrWhiteSpace(template.Category)
            && !string.IsNullOrWhiteSpace(template.Dimensions)
            && !string.IsNullOrWhiteSpace(template.Description)));
    }

    [TestMethod]
    public void SelectedTemplateLoadsAsDetachedModifiedDocument()
    {
        SvgTemplateDefinition template =
            new SvgTemplateCatalog().LoadAll().First();
        MainViewModel viewModel = new();

        viewModel.LoadDocument(
            template.Source,
            path: null,
            isModified: true);

        Assert.IsNull(viewModel.CurrentFilePath);
        Assert.AreEqual("Untitled.svg", viewModel.CurrentFileName);
        Assert.IsTrue(viewModel.IsModified);
        Assert.AreEqual("Modified", viewModel.SaveStatus);
    }

    [TestMethod]
    public void SavingDetachedCopyCannotModifyEmbeddedTemplate()
    {
        SvgTemplateCatalog catalog = new();
        SvgTemplateDefinition before = catalog.LoadAll()
            .Single(template => template.Id == "blank");
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Template.Tests-{Guid.NewGuid():N}");
        string savedPath = Path.Combine(directory, "copy.svg");
        try
        {
            Directory.CreateDirectory(directory);
            new Utf8FileService().WriteAllText(
                savedPath,
                before.Source.Replace(
                    "800 600",
                    "640 480",
                    StringComparison.Ordinal));

            SvgTemplateDefinition after = catalog.LoadAll()
                .Single(template => template.Id == "blank");
            Assert.AreEqual(before.Source, after.Source);
            Assert.AreNotEqual(
                after.Source,
                new Utf8FileService().ReadAllText(savedPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CancelledUnsavedPromptLeavesCurrentDocumentUntouched()
    {
        const string current =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>current</text></svg>";
        MainViewModel viewModel = new();
        viewModel.LoadDocument(current, path: null, isModified: true);

        bool canProceed = new UnsavedChangesPolicy().CanProceed(
            hasUnsavedChanges: true,
            UnsavedChangesChoice.Cancel,
            saveSucceeded: false);

        Assert.IsFalse(canProceed);
        Assert.AreEqual(current, viewModel.DocumentText);
        Assert.IsTrue(viewModel.IsModified);
        Assert.IsNull(viewModel.CurrentFilePath);
    }
}
