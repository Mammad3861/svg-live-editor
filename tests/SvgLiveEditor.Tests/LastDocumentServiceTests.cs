using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class LastDocumentServiceTests
{
    [TestMethod]
    public void OpenThenSaveAs_StoresAndUpdatesTheFullSupportedPath()
    {
        string directory = CreateTemporaryDirectory();
        string settingsPath = Path.Combine(directory, "settings.json");
        string openedPath = Path.Combine(directory, "opened.svg");
        string savedAsPath = Path.Combine(directory, "saved-as.txt");
        try
        {
            UserPreferencesService settings = new(settingsPath);
            LastDocumentService documents = new();
            UserPreferences preferences = documents.Remember(
                settings.Load(),
                openedPath);
            Assert.IsTrue(settings.TrySave(preferences));
            Assert.AreEqual(
                Path.GetFullPath(openedPath),
                settings.Load().LastDocumentPath);

            preferences = documents.Remember(
                settings.Load(),
                savedAsPath);
            Assert.IsTrue(settings.TrySave(preferences));
            Assert.AreEqual(
                Path.GetFullPath(savedAsPath),
                settings.Load().LastDocumentPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void StartupRestore_ReadsExactPersianUtf8FromSupportedFile()
    {
        string directory = CreateTemporaryDirectory();
        string svgPath = Path.Combine(directory, "persian.svg");
        const string source =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام فارسی</text></svg>\r\n";
        try
        {
            new Utf8FileService().WriteAllText(svgPath, source);
            UserPreferences preferences =
                new LastDocumentService().Remember(
                    UserPreferences.Default,
                    svgPath);

            LastDocumentRestoreResult result =
                new LastDocumentService().TryRestore(preferences);

            Assert.IsTrue(result.IsRestored);
            Assert.IsFalse(result.ShouldClearPath);
            Assert.AreEqual(Path.GetFullPath(svgPath), result.Path);
            Assert.AreEqual(source, result.Source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingAndUnsupportedPaths_FallBackAndRequestPathClearing()
    {
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor-missing-{Guid.NewGuid():N}.svg");
        UserPreferences missingPreferences =
            UserPreferences.Default with { LastDocumentPath = missing };
        UserPreferences unsupportedPreferences =
            UserPreferences.Default with
            {
                LastDocumentPath = Path.ChangeExtension(missing, ".png")
            };

        LastDocumentService service = new();
        LastDocumentRestoreResult missingResult =
            service.TryRestore(missingPreferences);
        LastDocumentRestoreResult unsupportedResult =
            service.TryRestore(unsupportedPreferences);

        Assert.IsFalse(missingResult.IsRestored);
        Assert.IsTrue(missingResult.ShouldClearPath);
        Assert.IsFalse(unsupportedResult.IsRestored);
        Assert.IsTrue(unsupportedResult.ShouldClearPath);
    }

    [TestMethod]
    public void InaccessibleFile_FallsBackWithoutThrowing()
    {
        string directory = CreateTemporaryDirectory();
        string svgPath = Path.Combine(directory, "locked.svg");
        File.WriteAllText(
            svgPath,
            "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        try
        {
            using FileStream lockStream = new(
                svgPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            UserPreferences preferences =
                UserPreferences.Default with { LastDocumentPath = svgPath };

            LastDocumentRestoreResult result =
                new LastDocumentService().TryRestore(preferences);

            Assert.IsFalse(result.IsRestored);
            Assert.IsTrue(result.ShouldClearPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void DisabledPreference_OpensWelcomePathWithoutForgettingLastDocument()
    {
        string lastPath = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor-{Guid.NewGuid():N}.svg");
        UserPreferences preferences = UserPreferences.Default with
        {
            ReopenLastDocumentOnStartup = false,
            LastDocumentPath = lastPath
        };

        LastDocumentRestoreResult result =
            new LastDocumentService().TryRestore(preferences);

        Assert.AreEqual(LastDocumentRestoreResult.NotRequested, result);
        Assert.AreEqual(lastPath, preferences.LastDocumentPath);
        StringAssert.Contains(new WelcomeSvgProvider().Load(), "<svg");
    }

    [TestMethod]
    public void Forget_ClearsOnlyTheLastPath()
    {
        UserPreferences preferences = new(
            WordWrap: false,
            PreviewZoom: new PreviewZoomState(
                PreviewZoomMode.Manual,
                1.25))
        {
            ReopenLastDocumentOnStartup = true,
            LastDocumentPath = Path.Combine(
                Path.GetTempPath(),
                "remembered.svg")
        };

        UserPreferences cleared =
            new LastDocumentService().Forget(preferences);

        Assert.IsNull(cleared.LastDocumentPath);
        Assert.IsFalse(cleared.WordWrap);
        Assert.AreEqual(preferences.PreviewZoom, cleared.PreviewZoom);
        Assert.IsTrue(cleared.ReopenLastDocumentOnStartup);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"SvgLiveEditor.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
