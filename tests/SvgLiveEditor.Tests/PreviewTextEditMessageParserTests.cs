using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewTextEditMessageParserTests
{
    private const string Token = "00112233445566778899AABBCCDDEEFF";
    private const string SelectionId = "11223344556677889900AABBCCDDEEFF";
    private const string SessionId = "FFEEDDCCBBAA00998877665544332211";
    private readonly PreviewTextEditMessageParser _parser = new();

    [TestMethod]
    public void ExactBeginCommitAndCancelSchemasAreAccepted()
    {
        Assert.IsTrue(_parser.TryParse(
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"begin","selectionId":"{{SelectionId}}"}""",
            Token,
            7,
            out PreviewTextEditMessage begin));
        Assert.AreEqual(PreviewTextEditAction.Begin, begin.Action);

        Assert.IsTrue(_parser.TryParse(
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"commit","selectionId":"{{SelectionId}}","sessionId":"{{SessionId}}","restorePreviewFocus":true,"value":"سلام & <SVG>"}""",
            Token,
            7,
            out PreviewTextEditMessage commit));
        Assert.AreEqual(PreviewTextEditAction.Commit, commit.Action);
        Assert.AreEqual("سلام & <SVG>", commit.Value);
        Assert.IsTrue(commit.RestorePreviewFocus);

        Assert.IsTrue(_parser.TryParse(
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"cancel","selectionId":"{{SelectionId}}","sessionId":"{{SessionId}}","restorePreviewFocus":false}""",
            Token,
            7,
            out PreviewTextEditMessage cancel));
        Assert.AreEqual(PreviewTextEditAction.Cancel, cancel.Action);
        Assert.IsFalse(cancel.RestorePreviewFocus);
    }

    [TestMethod]
    [DataRow(@"\uD800")]
    [DataRow(@"\uDC00")]
    public void IsolatedSurrogateEscapesFailClosed(string escapedValue)
    {
        // Keep the JSON escapes raw so no serializer replaces malformed Unicode.
        string json =
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"commit","selectionId":"{{SelectionId}}","sessionId":"{{SessionId}}","restorePreviewFocus":false,"value":"{{escapedValue}}"}""";

        Assert.IsFalse(_parser.TryParse(json, Token, 7, out PreviewTextEditMessage message));
        Assert.IsNull(message);
    }

    [TestMethod]
    public void ValidSurrogatePairEscapesPreserveExactUnicode()
    {
        string json =
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"commit","selectionId":"{{SelectionId}}","sessionId":"{{SessionId}}","restorePreviewFocus":false,"value":"سلام \uD83D\uDE00"}""";

        Assert.IsTrue(_parser.TryParse(json, Token, 7, out PreviewTextEditMessage message));
        Assert.AreEqual("سلام 😀", message.Value);
    }

    [TestMethod]
    public void StaleTokenRevisionIdentitySchemaAndPayloadFailClosed()
    {
        string valid =
            $$"""{"type":"textEdit","token":"{{Token}}","sourceRevision":7,"action":"commit","selectionId":"{{SelectionId}}","sessionId":"{{SessionId}}","restorePreviewFocus":false,"value":"valid"}""";

        Assert.IsFalse(_parser.TryParse(valid, "AABBCCDDEEFF00112233445566778899", 7, out _));
        Assert.IsFalse(_parser.TryParse(valid, Token, 8, out _));
        Assert.IsFalse(_parser.TryParse(valid.Replace(SelectionId, "bad", StringComparison.Ordinal), Token, 7, out _));
        Assert.IsFalse(_parser.TryParse(valid.Replace(SessionId, "bad", StringComparison.Ordinal), Token, 7, out _));
        Assert.IsFalse(_parser.TryParse(valid.Replace("\"value\":\"valid\"", "\"value\":\"valid\",\"extra\":true", StringComparison.Ordinal), Token, 7, out _));
        Assert.IsFalse(_parser.TryParse(valid.Replace("valid", "line\\nbreak", StringComparison.Ordinal), Token, 7, out _));
        Assert.IsFalse(_parser.TryParse(
            valid.Replace("valid", new string('x', SvgVisualTextIndexService.MaximumTextLength + 1), StringComparison.Ordinal),
            Token,
            7,
            out _));
    }
}
