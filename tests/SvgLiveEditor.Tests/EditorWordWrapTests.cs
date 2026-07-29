using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class EditorWordWrapTests
{
    [TestMethod]
    public void TogglingWordWrap_DoesNotAlterDocumentText()
    {
        const string source = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام SVG — exact UTF-8 source</text></svg>\r\n";
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                TextEditor editor = new()
                {
                    Text = source,
                    WordWrap = true,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
                };

                editor.WordWrap = false;
                editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                editor.WordWrap = true;
                editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;

                Assert.AreEqual(source, editor.Text);
                Assert.AreEqual(2, editor.Document.LineCount);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [TestMethod]
    public void Preference_DefaultsOnAndRoundTripsOutsideTheRepository()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"SvgLiveEditor.Tests-{Guid.NewGuid():N}");
        string settingsPath = Path.Combine(directory, "settings.json");

        try
        {
            UserPreferencesService service = new(settingsPath);
            UserPreferences defaults = service.Load();
            Assert.IsTrue(defaults.WordWrap);
            Assert.AreEqual(PreviewZoomMode.Fit, defaults.PreviewZoom.Mode);
            Assert.IsFalse(defaults.AutoSaveEnabled);
            Assert.IsTrue(defaults.ReopenLastDocumentOnStartup);
            Assert.IsNull(defaults.LastDocumentPath);

            UserPreferences changed = new(
                WordWrap: false,
                PreviewZoom: new PreviewZoomState(PreviewZoomMode.Manual, 1.25))
            {
                AutoSaveEnabled = true,
                ReopenLastDocumentOnStartup = false,
                LastDocumentPath = Path.Combine(directory, "sample.svg")
            };
            Assert.IsTrue(service.TrySave(changed));

            UserPreferences restored = service.Load();
            Assert.IsFalse(restored.WordWrap);
            Assert.AreEqual(PreviewZoomMode.Manual, restored.PreviewZoom.Mode);
            Assert.AreEqual(1.25, restored.PreviewZoom.ManualScale, 0.0001);
            Assert.IsTrue(restored.AutoSaveEnabled);
            Assert.IsFalse(restored.ReopenLastDocumentOnStartup);
            Assert.AreEqual(changed.LastDocumentPath, restored.LastDocumentPath);
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
    public void LegacyWordWrapOnlySettings_DefaultZoomToFit()
    {
        RunWithSettings("""{"wordWrap":false}""", preferences =>
        {
            Assert.IsFalse(preferences.WordWrap);
            Assert.AreEqual(PreviewZoomState.Fit, preferences.PreviewZoom);
            Assert.IsTrue(preferences.ReopenLastDocumentOnStartup);
            Assert.IsNull(preferences.LastDocumentPath);
        });
    }

    [TestMethod]
    public void InvalidZoomSettings_FallBackToFitWithoutDiscardingWordWrap()
    {
        RunWithSettings(
            """{"wordWrap":false,"previewZoomMode":"Manual","previewZoomPercent":"huge"}""",
            preferences =>
            {
                Assert.IsFalse(preferences.WordWrap);
                Assert.AreEqual(PreviewZoomState.Fit, preferences.PreviewZoom);
            });
        RunWithSettings(
            """{"wordWrap":true,"previewZoomMode":"Unexpected","previewZoomPercent":125}""",
            preferences => Assert.AreEqual(PreviewZoomState.Fit, preferences.PreviewZoom));
    }

    [TestMethod]
    public void CorruptSettings_FallBackToNewInstallationDefaults()
    {
        RunWithSettings(
            """{"wordWrap":false,"previewZoomMode":""",
            preferences => Assert.AreEqual(UserPreferences.Default, preferences));
    }

    [TestMethod]
    public void MalformedRecentDocumentSettings_AreIgnoredSafely()
    {
        RunWithSettings(
            """
            {
              "wordWrap": true,
              "reopenLastDocumentOnStartup": "sometimes",
              "lastDocumentPath": 42
            }
            """,
            preferences =>
            {
                Assert.IsTrue(preferences.ReopenLastDocumentOnStartup);
                Assert.IsNull(preferences.LastDocumentPath);
            });
    }

    [TestMethod]
    public void OutOfRangeManualZoom_IsClampedToSupportedLimits()
    {
        RunWithSettings(
            """{"wordWrap":true,"previewZoomMode":"Manual","previewZoomPercent":10}""",
            preferences => Assert.AreEqual(
                PreviewZoomCalculator.MinimumScale,
                preferences.PreviewZoom.ManualScale,
                0.0001));
        RunWithSettings(
            """{"wordWrap":true,"previewZoomMode":"Manual","previewZoomPercent":900}""",
            preferences => Assert.AreEqual(
                PreviewZoomCalculator.MaximumScale,
                preferences.PreviewZoom.ManualScale,
                0.0001));
    }

    private static void RunWithSettings(
        string json,
        Action<UserPreferences> assertion)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"SvgLiveEditor.Tests-{Guid.NewGuid():N}");
        string settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(settingsPath, json);
            assertion(new UserPreferencesService(settingsPath).Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
