namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ReleaseWorkflowTests
{
    private static string ReadWorkflow()
    {
        return File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "workflows",
                "release.yml"));
    }

    [TestMethod]
    public void MissingReleaseUsesGeneratedNotesAndStaysDraft()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(workflow, "'--generate-notes'");
        StringAssert.Contains(workflow, "'--notes-start-tag'");
        StringAssert.Contains(workflow, "'--draft'");
        StringAssert.Contains(workflow, "'--verify-tag'");
        Assert.IsFalse(workflow.Contains(
            "Automated binary package for",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ExistingReleaseNotesAreNeverEdited()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(workflow, "gh release upload");
        StringAssert.Contains(workflow, "--clobber");
        Assert.IsFalse(workflow.Contains(
            "gh release edit",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(workflow.Contains(
            "gh api --method PATCH",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void PublicReleaseUploadContainsOnlyTheZip()
    {
        string workflow = ReadWorkflow();
        int attachStep = workflow.IndexOf(
            "- name: Attach assets to the matching GitHub Release",
            StringComparison.Ordinal);
        Assert.IsTrue(attachStep >= 0);
        string releaseUpload = workflow[attachStep..];

        StringAssert.Contains(
            releaseUpload,
            "gh release upload $tag $archivePath");
        Assert.IsFalse(releaseUpload.Contains(
            "$checksumPath",
            StringComparison.Ordinal));
        Assert.IsFalse(releaseUpload.Contains(
            ".sha256",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InternalChecksumIsGeneratedVerifiedAndSummarized()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(
            workflow,
            "SvgLiveEditor-$env:RELEASE_TAG-win-x64.sha256");
        StringAssert.Contains(
            workflow,
            "$actualChecksum -cne $expectedChecksum");
        StringAssert.Contains(
            workflow,
            "Verified release ZIP SHA-256");
        StringAssert.Contains(workflow, "GITHUB_STEP_SUMMARY");
    }

    [TestMethod]
    public void StableTagValidationAndExactCheckoutRemainRequired()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(
            workflow,
            "^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$");
        StringAssert.Contains(workflow, "git checkout --detach \"refs/tags/$tag\"");
        StringAssert.Contains(workflow, "$checkedOutCommit -cne $tagCommit");
    }
}
