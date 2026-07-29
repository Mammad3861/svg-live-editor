using System.IO;
using System.Text;
using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class UserPreferencesService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly string _settingsPath;

    public UserPreferencesService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SvgLiveEditor",
            "settings.json"))
    {
    }

    public UserPreferencesService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return UserPreferences.Default;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_settingsPath, Encoding.UTF8));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return UserPreferences.Default;
            }

            bool wordWrap = root.TryGetProperty("wordWrap", out JsonElement value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : true;
            bool reopenLastDocument = root.TryGetProperty(
                    "reopenLastDocumentOnStartup",
                    out JsonElement reopenValue)
                && reopenValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? reopenValue.GetBoolean()
                    : true;
            string? lastDocumentPath = root.TryGetProperty(
                    "lastDocumentPath",
                    out JsonElement pathValue)
                && pathValue.ValueKind == JsonValueKind.String
                    ? pathValue.GetString()
                    : null;
            bool autoSaveEnabled = root.TryGetProperty(
                    "autoSaveEnabled",
                    out JsonElement autoSaveValue)
                && autoSaveValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? autoSaveValue.GetBoolean()
                    : false;
            return new UserPreferences(wordWrap, ReadPreviewZoom(root))
            {
                AutoSaveEnabled = autoSaveEnabled,
                ReopenLastDocumentOnStartup = reopenLastDocument,
                LastDocumentPath = string.IsNullOrWhiteSpace(lastDocumentPath)
                    ? null
                    : lastDocumentPath
            };
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            return UserPreferences.Default;
        }
    }

    public bool LoadWordWrap() => Load().WordWrap;

    public bool TrySave(UserPreferences preferences)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(new
            {
                wordWrap = preferences.WordWrap,
                previewZoomMode = preferences.PreviewZoom.Mode.ToString(),
                previewZoomPercent = preferences.PreviewZoom.ManualScale * 100,
                autoSaveEnabled = preferences.AutoSaveEnabled,
                reopenLastDocumentOnStartup =
                    preferences.ReopenLastDocumentOnStartup,
                lastDocumentPath = preferences.LastDocumentPath
            });
            File.WriteAllText(_settingsPath, json, Utf8WithoutBom);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TrySaveWordWrap(bool enabled)
    {
        UserPreferences current = Load();
        return TrySave(current with { WordWrap = enabled });
    }

    private static PreviewZoomState ReadPreviewZoom(JsonElement root)
    {
        if (!root.TryGetProperty("previewZoomMode", out JsonElement modeValue))
        {
            // Compatibility with settings written before zoom persistence existed.
            return PreviewZoomState.Fit;
        }

        if (modeValue.ValueKind != JsonValueKind.String)
        {
            return PreviewZoomState.Fit;
        }

        string? mode = modeValue.GetString();
        if (string.Equals(mode, nameof(PreviewZoomMode.Fit), StringComparison.OrdinalIgnoreCase))
        {
            return PreviewZoomState.Fit;
        }

        if (!string.Equals(mode, nameof(PreviewZoomMode.Manual), StringComparison.OrdinalIgnoreCase)
            || !root.TryGetProperty("previewZoomPercent", out JsonElement percentValue)
            || percentValue.ValueKind != JsonValueKind.Number
            || !percentValue.TryGetDouble(out double percent)
            || !double.IsFinite(percent)
            || percent <= 0)
        {
            return PreviewZoomState.Fit;
        }

        double scale = Math.Clamp(
            percent / 100,
            PreviewZoomCalculator.MinimumScale,
            PreviewZoomCalculator.MaximumScale);
        return new PreviewZoomState(PreviewZoomMode.Manual, scale);
    }
}
