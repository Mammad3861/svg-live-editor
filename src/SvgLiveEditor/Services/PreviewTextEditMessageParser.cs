using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewTextEditMessageParser
{
    private const int MaximumMessageLength = 8_192;

    public bool TryParse(
        string json,
        string expectedToken,
        long expectedSourceRevision,
        out PreviewTextEditMessage message)
    {
        message = null!;
        if (string.IsNullOrWhiteSpace(json)
            || json.Length > MaximumMessageLength
            || expectedSourceRevision < 0)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryReadString(root, "type", out string? type)
                || type != "textEdit"
                || !TryReadString(root, "token", out string? token)
                || !token.Equals(expectedToken, StringComparison.Ordinal)
                || !TryReadRevision(
                    root,
                    expectedSourceRevision,
                    out long sourceRevision)
                || !TryReadHex(root, "selectionId", out string? selectionId)
                || !TryReadString(root, "action", out string? action))
            {
                return false;
            }

            if (action == "begin" && root.EnumerateObject().Count() == 5)
            {
                message = new PreviewTextEditMessage(
                    PreviewTextEditAction.Begin,
                    sourceRevision,
                    selectionId,
                    SessionId: null,
                    Value: null,
                    RestorePreviewFocus: false);
                return true;
            }
            if (action == "cancel"
                && root.EnumerateObject().Count() == 7
                && TryReadHex(root, "sessionId", out string? cancelSession)
                && TryReadBoolean(
                    root,
                    "restorePreviewFocus",
                    out bool cancelRestoreFocus))
            {
                message = new PreviewTextEditMessage(
                    PreviewTextEditAction.Cancel,
                    sourceRevision,
                    selectionId,
                    cancelSession,
                    Value: null,
                    cancelRestoreFocus);
                return true;
            }
            if (action == "commit"
                && root.EnumerateObject().Count() == 8
                && TryReadHex(root, "sessionId", out string? commitSession)
                && TryReadBoolean(
                    root,
                    "restorePreviewFocus",
                    out bool commitRestoreFocus)
                && TryReadString(root, "value", out string? value)
                && value.Length <= SvgVisualTextIndexService.MaximumTextLength
                && !value.Any(char.IsControl))
            {
                message = new PreviewTextEditMessage(
                    PreviewTextEditAction.Commit,
                    sourceRevision,
                    selectionId,
                    commitSession,
                    value,
                    commitRestoreFocus);
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryReadRevision(
        JsonElement root,
        long expected,
        out long revision)
    {
        revision = -1;
        return root.TryGetProperty("sourceRevision", out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out revision)
            && revision == expected;
    }

    private static bool TryReadHex(
        JsonElement root,
        string name,
        out string value) =>
        TryReadString(root, name, out value)
        && value.Length == 32
        && value.All(Uri.IsHexDigit);

    private static bool TryReadString(
        JsonElement root,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        try
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }
        catch (InvalidOperationException)
        {
            // Malformed surrogate escapes fail when the JSON string is decoded.
            return false;
        }
    }

    private static bool TryReadBoolean(
        JsonElement root,
        string name,
        out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }
}
