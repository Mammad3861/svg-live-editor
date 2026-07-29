using System.Text;
using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgFontFamilyWorkflowTests
{
    private readonly SvgDocumentIndexService _indexService = new();

    [TestMethod]
    public void SuggestedFamilyReplacesOnlyTheFirstExistingFamily()
    {
        SvgFontFamilyStackService service = new();

        Assert.IsTrue(service.TryCreateForSuggestion(
            "Segoe UI, sans-serif",
            "Tahoma",
            out string value));

        Assert.AreEqual("Tahoma, sans-serif", value);
    }

    [TestMethod]
    public void SuggestedFamilyBuildsQuotedSafeLocalFallbackStack()
    {
        SvgFontFamilyStackService service = new();

        Assert.IsTrue(service.TryCreateForSuggestion(
            string.Empty,
            "A Long Installed Font",
            out string value));

        Assert.AreEqual(
            "\"A Long Installed Font\", \"Segoe UI\", Tahoma, sans-serif",
            value);
        Assert.IsNull(SvgFontFamilyValueValidator.Validate(value));
    }

    [TestMethod]
    public void SuggestedFamilyPreservesQuotedFallbacksAndAvoidsDuplicates()
    {
        SvgFontFamilyStackService service = new();

        Assert.IsTrue(service.TryCreateForSuggestion(
            "\"First Font\", Tahoma, \"Noto Sans\", sans-serif",
            "Tahoma",
            out string value));

        Assert.AreEqual("Tahoma, \"Noto Sans\", sans-serif", value);
    }

    [TestMethod]
    public void GenericSuggestionRemainsAValidTerminalFamily()
    {
        Assert.IsTrue(new SvgFontFamilyStackService()
            .TryCreateForSuggestion(
                "Segoe UI, Tahoma, sans-serif",
                "serif",
                out string value));

        Assert.AreEqual("serif", value);
    }

    [TestMethod]
    public void GlyphCoverageChecksPersianLatinDigitsAndPunctuation()
    {
        const string text = "Hello سلام 123 ۱۲۳،.";
        const string persianOnly = "فقط فارسی ۱۲۳!";
        const string englishOnly = "English only 123!";
        HashSet<int> complete =
            $"{text}{persianOnly}{englishOnly}".EnumerateRunes()
            .Where(rune => !Rune.IsWhiteSpace(rune))
            .Select(rune => rune.Value)
            .ToHashSet();
        InstalledFontGlyphCoverageService service =
            new(_ => complete);

        Assert.AreEqual(
            FontGlyphCoverage.Complete,
            service.Check("Complete Font", text));
        Assert.AreEqual(
            FontGlyphCoverage.Complete,
            service.Check("Complete Font", persianOnly));
        Assert.AreEqual(
            FontGlyphCoverage.Complete,
            service.Check("Complete Font", englishOnly));
    }

    [TestMethod]
    public void GlyphCoverageWarnsForMissingPersianOrLatinWithoutRejecting()
    {
        HashSet<int> latinOnly = "Hello 123!.".EnumerateRunes()
            .Where(rune => !Rune.IsWhiteSpace(rune))
            .Select(rune => rune.Value)
            .ToHashSet();
        HashSet<int> persianOnly = "سلام ۱۲۳،.".EnumerateRunes()
            .Where(rune => !Rune.IsWhiteSpace(rune))
            .Select(rune => rune.Value)
            .ToHashSet();

        Assert.AreEqual(
            FontGlyphCoverage.Incomplete,
            new InstalledFontGlyphCoverageService(_ => latinOnly)
                .Check("Latin Font", "Hello سلام"));
        Assert.AreEqual(
            FontGlyphCoverage.Incomplete,
            new InstalledFontGlyphCoverageService(_ => persianOnly)
                .Check("Persian Font", "سلام Hello"));
        Assert.AreEqual(
            FontGlyphCoverage.Unknown,
            new InstalledFontGlyphCoverageService(_ => null)
                .Check("Unknown Font", "سلام Hello"));
    }

    [TestMethod]
    public void SelectionCommitIsOneUndoAndRefreshesVisualFontInput()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"10\" y=\"30\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\">سلام Hello</text></svg>";
        SvgElementNode text = FindText(source);
        Assert.IsTrue(new SvgFontFamilyStackService()
            .TryCreateForSuggestion(
                text.FindAttribute("font-family")!.RawValue,
                "Tahoma",
                out string stack));
        SvgAttributeEditResult edit = new SvgAttributeEditService()
            .CreateEdit(source, text, "font-family", stack);
        Assert.IsTrue(edit.IsSuccess, edit.ErrorMessage);
        TextDocument document = new(source);

        new AvalonEditDocumentEditService().Apply(
            document,
            edit.Edit!);

        const string expected =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"10\" y=\"30\" font-family=\"Tahoma, sans-serif\" font-size=\"20\">سلام Hello</text></svg>";
        Assert.AreEqual(expected, document.Text);
        SvgVisualElement refreshed = BuildVisual(document.Text)
            .Elements.Single(element =>
                element.SourceElement.Id == "label");
        Assert.AreEqual(
            "Tahoma, sans-serif",
            refreshed.TextMeasurement!.FontFamily);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(expected, document.Text);
    }

    [TestMethod]
    public void FontSelectionRoundTripsExactlyThroughSaveAutoSaveAndRecovery()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" direction=\"rtl\">سلام Hello ۱۲۳!</text></svg>";
        SvgElementNode text = FindText(source);
        Assert.IsTrue(new SvgFontFamilyStackService()
            .TryCreateForSuggestion(
                string.Empty,
                "A Long Installed Font",
                out string stack));
        SvgAttributeEditResult edit = new SvgAttributeEditService()
            .CreateEdit(source, text, "font-family", stack);
        Assert.IsTrue(edit.IsSuccess, edit.ErrorMessage);
        string expected = edit.Edit!.Apply(source);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.FontWorkflow-{Guid.NewGuid():N}");
        string documentPath = Path.Combine(directory, "mixed.svg");
        string recoveryPath = Path.Combine(directory, "Recovery");
        Utf8FileService fileService = new();

        try
        {
            Directory.CreateDirectory(directory);

            fileService.WriteAllText(documentPath, expected);
            Assert.AreEqual(expected, fileService.ReadAllText(documentPath));
            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(expected),
                File.ReadAllBytes(documentPath));

            fileService.WriteAllText(documentPath, source);
            AutoSavePrepareResult prepared =
                new AutoSaveFileService().Prepare(documentPath, expected);
            Assert.IsTrue(prepared.Succeeded, prepared.ErrorMessage);
            using (PreparedAutoSave autoSave = prepared.PreparedWrite!)
            {
                PersistenceOperationResult committed = autoSave.Commit();
                Assert.IsTrue(committed.Succeeded, committed.ErrorMessage);
            }
            Assert.AreEqual(expected, fileService.ReadAllText(documentPath));

            fileService.WriteAllText(documentPath, source);
            RecoverySnapshotStore recovery = new(
                recoveryPath,
                fileService,
                new SafeDocumentPathService());
            RecoverySnapshot snapshot = RecoverySnapshotStore.CreateSnapshot(
                RecoverySnapshotStore.CreateSnapshotId(),
                documentPath,
                "mixed.svg",
                expected,
                2,
                DateTimeOffset.UtcNow);
            PersistenceOperationResult recovered = recovery.TryWrite(snapshot);
            Assert.IsTrue(recovered.Succeeded, recovered.ErrorMessage);
            RecoveryCandidate candidate = recovery
                .LoadMeaningfulCandidates(DateTimeOffset.UtcNow)
                .Single();
            Assert.AreEqual(expected, candidate.Snapshot.Source);
            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(candidate.Snapshot.Source));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void QuotedStackIsXmlEscapedAndDecodedBackIntoProperties()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\">سلام Hello</text></svg>";
        SvgElementNode text = FindText(source);
        Assert.IsTrue(new SvgFontFamilyStackService()
            .TryCreateForSuggestion(
                string.Empty,
                "A Long Installed Font",
                out string stack));
        SvgAttributeEditResult edit = new SvgAttributeEditService()
            .CreateEdit(source, text, "font-family", stack);
        string updated = edit.Edit!.Apply(source);
        StringAssert.Contains(
            updated,
            "font-family=\"&quot;A Long Installed Font&quot;, &quot;Segoe UI&quot;, Tahoma, sans-serif\"");
        SvgElementNode updatedText = FindText(updated);
        SvgPropertyDefinition definition =
            SvgPropertySchema.Find("text", "font-family")!;
        SvgPropertyViewModel property = new(
            updatedText,
            definition,
            updatedText.FindAttribute("font-family"));

        Assert.AreEqual(stack, property.Value);
        Assert.AreEqual(stack, property.OriginalValue);
    }

    [TestMethod]
    public void EnterFocusLossEscapeAndDuplicateSuppressionKeepOneAttempt()
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text font-family=\"Tahoma\">Text</text></svg>";
        SvgElementNode text = FindText(source);
        SvgPropertyViewModel property = new(
            text,
            SvgPropertySchema.Find("text", "font-family")!,
            text.FindAttribute("font-family"));

        property.Value = "Arial";
        Assert.IsFalse(property.WasCurrentValueAlreadyAttempted);
        property.MarkCommitAttempt();
        Assert.IsTrue(property.WasCurrentValueAlreadyAttempted);
        property.MarkApplied();
        Assert.IsTrue(property.WasCurrentValueAlreadyAttempted);

        // A focus-loss event following Enter sees the same attempted value.
        Assert.AreEqual("Arial", property.OriginalValue);
        property.Value = "Tahoma";
        Assert.IsFalse(property.WasCurrentValueAlreadyAttempted);
        property.Revert();
        Assert.AreEqual("Arial", property.Value);
        Assert.IsFalse(property.WasCurrentValueAlreadyAttempted);
    }

    [TestMethod]
    [DataRow("24")]
    [DataRow("24px")]
    public void FontSizeEditRefreshesMeasurementAndIsOneUndo(
        string fontSize)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"label\" x=\"10\" y=\"30\" font-size=\"16\">Text</text></svg>";
        SvgAttributeEditResult edit = new SvgAttributeEditService()
            .CreateEdit(
                source,
                FindText(source),
                "font-size",
                fontSize);
        Assert.IsTrue(edit.IsSuccess, edit.ErrorMessage);
        TextDocument document = new(source);
        new AvalonEditDocumentEditService().Apply(document, edit.Edit!);

        SvgVisualElement refreshed = BuildVisual(document.Text)
            .Elements.Single();
        Assert.AreEqual(24, refreshed.TextMeasurement!.FontSize);
        document.UndoStack.Undo();
        Assert.AreEqual(source, document.Text);
        document.UndoStack.Redo();
        StringAssert.Contains(
            document.Text,
            $"font-size=\"{fontSize}\"");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("not-a-size")]
    public void InvalidFontSizeDoesNotDamageSource(string value)
    {
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text font-size=\"16\">Text</text></svg>";
        SvgAttributeEditResult result = new SvgAttributeEditService()
            .CreateEdit(
                source,
                FindText(source),
                "font-size",
                value);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Edit);
        Assert.AreEqual(
            source,
            new TextDocument(source).Text);
    }

    private SvgElementNode FindText(string source) =>
        _indexService.Build(source).Document!.Elements
            .Single(element => element.Name == "text");

    private SvgVisualDocument BuildVisual(string source)
    {
        SvgDocumentIndex document =
            _indexService.Build(source).Document!;
        return new SvgVisualGeometryIndexService().Build(
            document,
            new SvgCanvasSizeReader().Read(source),
            source);
    }
}
