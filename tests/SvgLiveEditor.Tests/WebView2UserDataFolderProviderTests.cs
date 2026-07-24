using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class WebView2UserDataFolderProviderTests
{
    [TestMethod]
    public void GetPath_KeepsRuntimeDataOutsideTheApplicationDirectory()
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "LocalApplicationData");
        WebView2UserDataFolderProvider provider = new(localApplicationData);

        string path = provider.GetPath();

        Assert.AreEqual(
            Path.Combine(localApplicationData, "SvgLiveEditor", "WebView2"),
            path);
        Assert.IsFalse(path.Contains("dist", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(path.EndsWith(".WebView2", StringComparison.OrdinalIgnoreCase));
    }
}
