using System.Text.Json;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewTextEditPageMessageTests
{
    private const string Token = "00112233445566778899AABBCCDDEEFF";
    private const string SessionId = "FFEEDDCCBBAA00998877665544332211";
    private const string SelectionId = "11223344556677889900AABBCCDDEEFF";
    private readonly PreviewPageMessageBuilder _builder = new();

    [TestMethod]
    public void CloseCommitRequestUsesOnlyBoundedSessionState()
    {
        using JsonDocument message = JsonDocument.Parse(
            _builder.BuildTextEditCommitMessage(Token, 9, SessionId));
        JsonElement root = message.RootElement;
        Assert.AreEqual(5, root.EnumerateObject().Count());
        Assert.AreEqual("textEditState", root.GetProperty("type").GetString());
        Assert.AreEqual("commit", root.GetProperty("action").GetString());
        Assert.AreEqual(Token, root.GetProperty("token").GetString());
        Assert.AreEqual(9, root.GetProperty("sourceRevision").GetInt64());
        Assert.AreEqual(SessionId, root.GetProperty("sessionId").GetString());
        using JsonDocument resume = JsonDocument.Parse(
            _builder.BuildTextEditResumeMessage(Token, 9, SessionId, SelectionId));
        JsonElement resumed = resume.RootElement;
        Assert.AreEqual(6, resumed.EnumerateObject().Count());
        Assert.AreEqual("textEditState", resumed.GetProperty("type").GetString());
        Assert.AreEqual("resume", resumed.GetProperty("action").GetString());
        Assert.AreEqual(Token, resumed.GetProperty("token").GetString());
        Assert.AreEqual(9, resumed.GetProperty("sourceRevision").GetInt64());
        Assert.AreEqual(SessionId, resumed.GetProperty("sessionId").GetString());
        Assert.AreEqual(SelectionId, resumed.GetProperty("nextSessionId").GetString());
        Assert.Throws<ArgumentException>(() => _builder.BuildTextEditResumeMessage("bad", 9, SessionId, SelectionId));
        Assert.Throws<ArgumentException>(() => _builder.BuildTextEditResumeMessage(Token, 9, "bad", SelectionId));
        Assert.Throws<ArgumentException>(() => _builder.BuildTextEditResumeMessage(Token, 9, SessionId, "bad"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.BuildTextEditResumeMessage(Token, -1, SessionId, SelectionId));
        Assert.Throws<ArgumentException>(() => _builder.BuildTextEditCommitMessage("bad", 9, SessionId));
        Assert.Throws<ArgumentException>(() => _builder.BuildTextEditCommitMessage(Token, 9, "bad"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.BuildTextEditCommitMessage(Token, -1, SessionId));
    }

    [TestMethod]
    public void OpenAndCloseUseExactBoundedHostSchemas()
    {
        using JsonDocument open = JsonDocument.Parse(
            _builder.BuildTextEditOpenMessage(
                Token,
                9,
                SessionId,
                SelectionId,
                "سلام & <SVG>",
                "rtl"));
        JsonElement root = open.RootElement;
        Assert.AreEqual(8, root.EnumerateObject().Count());
        Assert.AreEqual("textEditState", root.GetProperty("type").GetString());
        Assert.AreEqual(Token, root.GetProperty("token").GetString());
        Assert.AreEqual(9, root.GetProperty("sourceRevision").GetInt64());
        Assert.AreEqual("open", root.GetProperty("action").GetString());
        Assert.AreEqual(SessionId, root.GetProperty("sessionId").GetString());
        Assert.AreEqual(SelectionId, root.GetProperty("selectionId").GetString());
        Assert.AreEqual("سلام & <SVG>", root.GetProperty("value").GetString());
        Assert.AreEqual("rtl", root.GetProperty("direction").GetString());

        using JsonDocument close = JsonDocument.Parse(
            _builder.BuildTextEditCloseMessage(
                Token,
                9,
                SessionId,
                restorePreviewFocus: true));
        Assert.AreEqual(6, close.RootElement.EnumerateObject().Count());
        Assert.AreEqual("close", close.RootElement.GetProperty("action").GetString());
        Assert.IsTrue(close.RootElement.GetProperty("restorePreviewFocus").GetBoolean());
    }

    [TestMethod]
    public void InvalidHostTextStateCannotBeSerialized()
    {
        Assert.Throws<ArgumentException>(() =>
            _builder.BuildTextEditOpenMessage(
                "bad",
                1,
                SessionId,
                SelectionId,
                "text",
                "ltr"));
        Assert.Throws<ArgumentException>(() =>
            _builder.BuildTextEditOpenMessage(
                Token,
                1,
                "bad",
                SelectionId,
                "text",
                "ltr"));
        Assert.Throws<ArgumentException>(() =>
            _builder.BuildTextEditOpenMessage(
                Token,
                1,
                SessionId,
                "bad",
                "text",
                "ltr"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildTextEditOpenMessage(
                Token,
                1,
                SessionId,
                SelectionId,
                new string('x', SvgVisualTextIndexService.MaximumTextLength + 1),
                "ltr"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildTextEditOpenMessage(
                Token,
                1,
                SessionId,
                SelectionId,
                "line\nbreak",
                "ltr"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildTextEditOpenMessage(
                Token,
                1,
                SessionId,
                SelectionId,
                "text",
                "auto"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildTextEditCloseMessage(
                Token,
                -1,
                SessionId,
                restorePreviewFocus: false));
    }
}
