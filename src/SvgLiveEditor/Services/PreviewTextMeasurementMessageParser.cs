using System.Text.Json;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewTextMeasurementMessageParser
{
    public bool TryParse(
        string json,
        PendingPreviewTextMeasurement pending,
        out IReadOnlyList<SvgVisualTextMeasurementResult> results)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(pending);
        results = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 5
                || !ReadString(root, "type", out string? type)
                || type != "textMeasurements"
                || !ReadString(root, "token", out string? token)
                || !token!.Equals(pending.Token, StringComparison.Ordinal)
                || !ReadRevision(
                    root,
                    pending.SourceRevision,
                    out _)
                || !ReadString(
                    root,
                    "requestId",
                    out string? requestId)
                || !requestId!.Equals(
                    pending.RequestId,
                    StringComparison.Ordinal)
                || !root.TryGetProperty(
                    "results",
                    out JsonElement resultArray)
                || resultArray.ValueKind != JsonValueKind.Array
                || resultArray.GetArrayLength()
                    != pending.ExpectedIndices.Count)
            {
                return false;
            }

            HashSet<int> expected = pending.ExpectedIndices.ToHashSet();
            List<SvgVisualTextMeasurementResult> parsed = [];
            foreach (JsonElement item in resultArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || item.EnumerateObject().Count() != 6
                    || !ReadInteger(item, "index", out int index)
                    || !expected.Remove(index)
                    || !ReadBoolean(item, "success", out bool success)
                    || !ReadCoordinate(item, "left", out double left)
                    || !ReadCoordinate(item, "top", out double top)
                    || !ReadCoordinate(item, "right", out double right)
                    || !ReadCoordinate(item, "bottom", out double bottom)
                    || (success && (right <= left || bottom <= top))
                    || (!success
                        && (left != 0 || top != 0
                            || right != 0 || bottom != 0)))
                {
                    return false;
                }

                parsed.Add(new SvgVisualTextMeasurementResult(
                    index,
                    success,
                    success
                        ? new SvgVisualBounds(left, top, right, bottom)
                        : null));
            }

            if (expected.Count != 0)
            {
                return false;
            }

            results = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ReadRevision(
        JsonElement root,
        long expected,
        out long value)
    {
        value = 0;
        return root.TryGetProperty(
                "sourceRevision",
                out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value)
            && value == expected
            && value >= 0;
    }

    private static bool ReadString(
        JsonElement root,
        string name,
        out string? value)
    {
        value = null;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            && (value = property.GetString()) is not null;
    }

    private static bool ReadInteger(
        JsonElement root,
        string name,
        out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value is >= 0
                and < SvgVisualTextIndexService.MaximumMeasuredTextElements;
    }

    private static bool ReadBoolean(
        JsonElement root,
        string name,
        out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind
                is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool ReadCoordinate(
        JsonElement root,
        string name,
        out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value)
            && double.IsFinite(value)
            && Math.Abs(value)
                <= SvgVisualLengthParser.MaximumAbsoluteValue;
    }
}
