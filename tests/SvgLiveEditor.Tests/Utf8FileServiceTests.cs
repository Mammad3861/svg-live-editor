using System.Text;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class Utf8FileServiceTests
{
    private readonly Utf8FileService _service = new();
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"SvgLiveEditor.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void DeleteTemporaryDirectory()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void WriteAndRead_PreservesExactUnicodeSource()
    {
        string path = Path.Combine(_temporaryDirectory, "unicode.svg");
        const string source = "<svg xmlns=\"http://www.w3.org/2000/svg\">\r\n  <text>سلام SVG — 你好</text>\r\n</svg>";

        _service.WriteAllText(path, source);
        string result = _service.ReadAllText(path);

        Assert.AreEqual(source, result);
    }

    [TestMethod]
    public void WriteAllText_UsesUtf8WithoutBom()
    {
        string path = Path.Combine(_temporaryDirectory, "plain.svg");

        _service.WriteAllText(path, "سلام");
        byte[] bytes = File.ReadAllBytes(path);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("سلام"), bytes);
    }

    [TestMethod]
    public void ReadAllText_AcceptsUtf8BomWithoutReturningIt()
    {
        string path = Path.Combine(_temporaryDirectory, "bom.svg");
        byte[] content = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes("سلام")).ToArray();
        File.WriteAllBytes(path, content);

        string result = _service.ReadAllText(path);

        Assert.AreEqual("سلام", result);
    }

    [TestMethod]
    public void ReadAllText_RejectsInvalidUtf8()
    {
        string path = Path.Combine(_temporaryDirectory, "invalid.svg");
        File.WriteAllBytes(path, [0xC3, 0x28]);

        Assert.ThrowsExactly<DecoderFallbackException>(() => _service.ReadAllText(path));
    }
}
