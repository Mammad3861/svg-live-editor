using System.Text.Json;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewTextMeasurementTests
{
    private const string Token = "00112233445566778899AABBCCDDEEFF";
    private const string RequestId = "FFEEDDCCBBAA99887766554433221100";

    [TestMethod]
    public void RequestBuilderUsesExactBoundedSchema()
    {
        string json = new PreviewPageMessageBuilder()
            .BuildTextMeasurementMessage(
                Token,
                9,
                RequestId,
                [CreateSpec(0)]);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement item = root.GetProperty("items")[0];

        Assert.AreEqual(5, root.EnumerateObject().Count());
        Assert.AreEqual("measureText", root.GetProperty("type").GetString());
        Assert.AreEqual(11, item.EnumerateObject().Count());
        Assert.AreEqual("سلام SVG", item.GetProperty("text").GetString());
        Assert.AreEqual("Tahoma, sans-serif", item.GetProperty("fontFamily").GetString());
    }

    [TestMethod]
    public void ResultParserAcceptsOnlyMatchingTokenRevisionRequestAndIndices()
    {
        PendingPreviewTextMeasurement pending = new(
            Token,
            9,
            RequestId,
            [0]);
        PreviewTextMeasurementMessageParser parser = new();
        const string valid =
            """{"type":"textMeasurements","token":"00112233445566778899AABBCCDDEEFF","sourceRevision":9,"requestId":"FFEEDDCCBBAA99887766554433221100","results":[{"index":0,"success":true,"left":10,"top":20,"right":80,"bottom":50}]}""";

        Assert.IsTrue(parser.TryParse(
            valid,
            pending,
            out IReadOnlyList<SvgVisualTextMeasurementResult> results));
        Assert.AreEqual(new SvgVisualBounds(10, 20, 80, 50), results[0].Bounds);
        Assert.IsFalse(parser.TryParse(
            valid.Replace("\"sourceRevision\":9", "\"sourceRevision\":8"),
            pending,
            out _));
        Assert.IsFalse(parser.TryParse(
            valid.Replace(Token, "10112233445566778899AABBCCDDEEFF"),
            pending,
            out _));
        Assert.IsFalse(parser.TryParse(
            valid.Replace("\"bottom\":50", "\"bottom\":50,\"extra\":1"),
            pending,
            out _));
        Assert.IsFalse(parser.TryParse(
            valid.Replace("\"index\":0", "\"index\":1"),
            pending,
            out _));
    }

    [TestMethod]
    public void BuilderRejectsInjectionShapedFontFamily()
    {
        SvgVisualTextMeasurementSpec invalid = CreateSpec(0) with
        {
            FontFamily = "Arial; background:url(https://example.test)"
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PreviewPageMessageBuilder().BuildTextMeasurementMessage(
                Token,
                9,
                RequestId,
                [invalid]));
    }

    private static SvgVisualTextMeasurementSpec CreateSpec(int index) =>
        new(
            index,
            "سلام SVG",
            120,
            80,
            24,
            "Tahoma, sans-serif",
            "700",
            "normal",
            "end",
            "rtl",
            "plaintext");
}
