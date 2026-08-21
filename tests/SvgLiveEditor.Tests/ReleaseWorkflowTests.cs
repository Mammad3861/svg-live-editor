using System.Text.RegularExpressions;

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

    private static string ReadPublishScript()
    {
        return File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "scripts",
                "Publish-WinX64.ps1"));
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

    [TestMethod]
    public void ReleaseToolingStagesEveryPackagingScriptDependencyBeforeCheckout()
    {
        string workflow = ReadWorkflow();
        string publishScript = ReadPublishScript();
        string[] siblingDependencies = Regex.Matches(
                publishScript,
                @"Join-Path\s+\$PSScriptRoot\s+'(?<name>[^']+\.ps1)'",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.Contains(siblingDependencies, "Test-AppIcon.ps1");

        const string toolingDirectoryAssignment =
            "$releaseToolingDirectory = Join-Path $env:RUNNER_TEMP "
            + "'SvgLiveEditor.ReleaseTooling'";
        int toolingStart = workflow.IndexOf(
            toolingDirectoryAssignment,
            StringComparison.Ordinal);
        int checkout = workflow.IndexOf(
            "git checkout --detach \"refs/tags/$tag\"",
            StringComparison.Ordinal);
        Assert.IsTrue(toolingStart >= 0 && checkout > toolingStart);
        string preCheckoutTooling = workflow[toolingStart..checkout];

        Match stagedScripts = Regex.Match(
            preCheckoutTooling,
            @"\$releaseToolingScripts\s*=\s*@\((?<body>.*?)\)",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.IsTrue(stagedScripts.Success);
        string[] stagedScriptNames = Regex.Matches(
                stagedScripts.Groups["body"].Value,
                @"'(?<name>[^']+\.ps1)'",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        string[] requiredScripts =
            ["Publish-WinX64.ps1", .. siblingDependencies];
        CollectionAssert.AreEquivalent(
            requiredScripts.Distinct(StringComparer.Ordinal).ToArray(),
            stagedScriptNames);

        StringAssert.Contains(
            preCheckoutTooling,
            "foreach ($scriptName in $releaseToolingScripts)");
        StringAssert.Contains(
            preCheckoutTooling,
            "-LiteralPath (Join-Path 'scripts' $scriptName)");
        StringAssert.Contains(
            preCheckoutTooling,
            "-Destination (Join-Path $releaseToolingDirectory $scriptName)");

        int packageStep = workflow.IndexOf(
            "- name: Build and audit release package",
            StringComparison.Ordinal);
        Assert.IsTrue(packageStep > checkout);
        string packageWorkflow = workflow[packageStep..];
        StringAssert.Contains(packageWorkflow, toolingDirectoryAssignment);
        StringAssert.Contains(
            packageWorkflow,
            "$packagingScript = Join-Path $releaseToolingDirectory 'Publish-WinX64.ps1'");
    }
}
