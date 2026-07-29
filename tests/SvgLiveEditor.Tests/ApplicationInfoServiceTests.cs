using System.Reflection;
using System.Runtime.InteropServices;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ApplicationInfoServiceTests
{
    [TestMethod]
    public void ProductionAssemblyVersion_FormatsAsProjectVersionWithoutBuildMetadata()
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        string informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.AreEqual(
            "v0.6.0",
            ApplicationInfoService.FormatVersion(informationalVersion));
    }

    [TestMethod]
    [DataRow("0.2.0+abc123", "v0.2.0")]
    [DataRow("0.2.0-beta.1+abc123", "v0.2.0-beta.1")]
    [DataRow("v0.2.0", "v0.2.0")]
    [DataRow("0.3.0+abc123", "v0.3.0")]
    [DataRow("0.3.1+abc123", "v0.3.1")]
    [DataRow("0.4.0+abc123", "v0.4.0")]
    [DataRow("0.6.0+abc123", "v0.6.0")]
    public void FormatVersion_RemovesOnlyBuildMetadata(
        string value,
        string expected)
    {
        Assert.AreEqual(expected, ApplicationInfoService.FormatVersion(value));
    }

    [TestMethod]
    public void Create_UsesAssemblyMetadataAndWindowsArchitecture()
    {
        var result = new ApplicationInfoService().Create(
            typeof(MainWindow).Assembly,
            Architecture.X64);

        Assert.AreEqual("SvgLiveEditor", result.Name);
        Assert.AreEqual("v0.6.0", result.Version);
        Assert.AreEqual("win-x64", result.Architecture);
        Assert.AreEqual(ApplicationInfoService.RepositoryUrl, result.RepositoryUrl);
        StringAssert.Contains(result.CopyText, "SvgLiveEditor v0.6.0 (win-x64)");
    }
}
