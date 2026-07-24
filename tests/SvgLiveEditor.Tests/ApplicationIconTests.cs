using System.Drawing;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class ApplicationIconTests
{
    [TestMethod]
    public void Ico_ContainsAllRequiredEmbeddedResolutions()
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SvgLiveEditor",
            "Assets",
            "SvgLiveEditor.ico"));
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);

        Assert.AreEqual(0, reader.ReadUInt16());
        Assert.AreEqual(1, reader.ReadUInt16());
        int count = reader.ReadUInt16();
        List<int> sizes = [];
        for (int index = 0; index < count; index++)
        {
            byte width = reader.ReadByte();
            byte height = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);
            Assert.AreEqual(width, height);
            stream.Position += 14;
        }

        CollectionAssert.AreEquivalent(
            new[] { 16, 24, 32, 48, 64, 128, 256 },
            sizes);
    }

    [TestMethod]
    public void ReleaseExecutable_ContainsAnEmbeddedApplicationIcon()
    {
        string executablePath = Path.Combine(
            Path.GetDirectoryName(typeof(MainWindow).Assembly.Location)!,
            "SvgLiveEditor.exe");

        Assert.IsTrue(File.Exists(executablePath));
        using Icon? icon = Icon.ExtractAssociatedIcon(executablePath);
        Assert.IsNotNull(icon);
        Assert.IsTrue(icon.Width >= 16);
        Assert.IsTrue(icon.Height >= 16);
    }
}
