using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PreviewNavigationPolicyTests
{
    private readonly PreviewNavigationPolicy _policy = new();

    [TestMethod]
    public void IsAllowed_AllowsOpaqueBlankOnlyForPendingHostPreview()
    {
        Assert.IsFalse(_policy.IsAllowed("about:blank", isHostPreviewRequested: false));
        Assert.IsTrue(_policy.IsAllowed("about:blank", isHostPreviewRequested: true));
        Assert.IsTrue(_policy.IsTrustedWebMessageSource("about:blank"));
        Assert.IsFalse(_policy.IsTrustedWebMessageSource("https://example.test/"));
    }

    [TestMethod]
    public void IsAllowed_AllowsDataHtmlOnlyForPendingHostPreview()
    {
        const string previewUri = "data:text/html;charset=utf-8;base64,PGh0bWw+PC9odG1sPg==";

        Assert.IsFalse(_policy.IsAllowed(previewUri, isHostPreviewRequested: false));
        Assert.IsTrue(_policy.IsAllowed(previewUri, isHostPreviewRequested: true));
        Assert.IsTrue(_policy.IsTrustedPreviewDocument(previewUri));
    }

    [TestMethod]
    [DataRow("https://example.test/")]
    [DataRow("file:///C:/secret.svg")]
    [DataRow("data:text/html,<script>bad()</script>")]
    [DataRow("data:image/svg+xml;base64,PHN2Zy8+")]
    public void IsAllowed_RejectsOtherNavigationEvenDuringHostPreview(string uri)
    {
        Assert.IsFalse(_policy.IsAllowed(uri, isHostPreviewRequested: true));
    }
}
