using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class SvgCanvasSizeReaderTests
{
    private readonly SvgCanvasSizeReader _reader = new();

    [TestMethod]
    public void Read_UsesWelcomeStyleViewBoxAsCanvasSize()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1040 440\" />";

        SvgCanvasSize size = _reader.Read(svg);

        Assert.AreEqual(new SvgCanvasSize(1040, 440), size);
    }

    [TestMethod]
    public void Read_UsesPixelDimensionsAndDerivesMissingDimensionFromViewBox()
    {
        const string both = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"640px\" height=\"320\" />";
        const string widthOnly = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" viewBox=\"0 0 200 100\" />";

        Assert.AreEqual(new SvgCanvasSize(640, 320), _reader.Read(both));
        Assert.AreEqual(new SvgCanvasSize(400, 200), _reader.Read(widthOnly));
    }

    [TestMethod]
    public void Read_UsesSvgDefaultWhenNoUsableSizeExists()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100%\" height=\"auto\" />";

        Assert.AreEqual(new SvgCanvasSize(300, 150), _reader.Read(svg));
    }
}
