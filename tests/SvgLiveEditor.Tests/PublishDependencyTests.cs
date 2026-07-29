namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PublishDependencyTests
{
    [TestMethod]
    public void CompositionControlRuntimeDependencyIsPresent()
    {
        string dependencyPath = Path.Combine(
            AppContext.BaseDirectory,
            "Microsoft.Windows.SDK.NET.dll");

        Assert.IsTrue(
            File.Exists(dependencyPath),
            "WebView2CompositionControl requires Microsoft.Windows.SDK.NET.dll at runtime.");
    }

    [TestMethod]
    public void PublishAuditRequiresCompositionControlRuntimeDependency()
    {
        string script = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "scripts",
                "Publish-WinX64.ps1"));

        Assert.AreEqual(
            2,
            CountOccurrences(script, "'Microsoft.Windows.SDK.NET.dll'"),
            "Both the publish-directory and ZIP audits must require the runtime dependency.");
    }

    [TestMethod]
    public void PublishProfileUsesExplicitWindowsSdkTarget()
    {
        string profile = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "publish",
                "win-x64.pubxml"));

        StringAssert.Contains(
            profile,
            "<TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>");
    }

    [TestMethod]
    public void PublishAuditRejectsRecoveryAndTemporaryState()
    {
        string script = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "scripts",
                "Publish-WinX64.ps1"));

        StringAssert.Contains(script, "$segment -ieq 'Recovery'");
        StringAssert.Contains(script, "'.tmp'");
        StringAssert.Contains(script, "$fileName -ieq 'settings.json'");
        StringAssert.Contains(script, "$segment -ieq 'EBWebView'");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
