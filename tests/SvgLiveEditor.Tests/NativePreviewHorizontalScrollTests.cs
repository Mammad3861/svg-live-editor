using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class NativePreviewHorizontalScrollTests
{
    private const string BridgeToken = "00112233445566778899AABBCCDDEEFF";
    private readonly NativeHorizontalWheelMessageParser _parser = new();
    private readonly PreviewNativeHorizontalScrollPolicy _policy = new();

    [TestMethod]
    [DataRow(120, 1600, 900)]
    [DataRow(-120, -120, -80)]
    [DataRow(30, 32767, -32768)]
    public void MouseHorizontalWheel_ParsesSignedDeltaAndScreenCoordinates(
        int delta,
        int x,
        int y)
    {
        bool parsed = _parser.TryParse(
            NativeHorizontalWheelMessageParser.MouseHorizontalWheelMessage,
            PackWheelParameters((short)delta, keyState: 0),
            PackScreenPoint((short)x, (short)y),
            out NativeHorizontalWheelInput input);

        Assert.IsTrue(parsed);
        Assert.AreEqual(delta, input.Delta);
        Assert.AreEqual(x, input.ScreenPoint.X);
        Assert.AreEqual(y, input.ScreenPoint.Y);
    }

    [TestMethod]
    public void NonMouseHorizontalZeroAndMalformedKeyState_AreRejected()
    {
        Assert.IsFalse(_parser.TryParse(
            NativeHorizontalWheelMessageParser.PointerHorizontalWheelMessage,
            PackWheelParameters(120, 0),
            PackScreenPoint(10, 20),
            out _));
        Assert.IsFalse(_parser.TryParse(
            NativeHorizontalWheelMessageParser.MouseHorizontalWheelMessage,
            PackWheelParameters(0, 0),
            PackScreenPoint(10, 20),
            out _));
        Assert.IsFalse(_parser.TryParse(
            NativeHorizontalWheelMessageParser.MouseHorizontalWheelMessage,
            PackWheelParameters(120, 0x8000),
            PackScreenPoint(10, 20),
            out _));
    }

    [TestMethod]
    public void PositiveNegativeAndPartialDeltas_KeepTheirDirectionAndPrecision()
    {
        PreviewNativeScrollContext context = ReadyContext();
        Assert.IsTrue(_policy.TryCreateRequest(
            new NativeHorizontalWheelInput(30, 0, new NativeScreenPoint(1, 1)),
            context,
            scrollCharacters: 3,
            out PreviewNativeScrollRequest partial));
        Assert.AreEqual(12, partial.DeltaX, 0.0001);

        Assert.IsTrue(_policy.TryCreateRequest(
            new NativeHorizontalWheelInput(-120, 0, new NativeScreenPoint(1, 1)),
            context,
            scrollCharacters: 3,
            out PreviewNativeScrollRequest negative));
        Assert.AreEqual(-48, negative.DeltaX, 0.0001);
    }

    [TestMethod]
    public void ExtremeAndPageDeltas_AreBoundedWithoutFullPageForSmallInput()
    {
        PreviewNativeScrollContext context = ReadyContext(viewportWidth: 500);
        Assert.IsTrue(_policy.TryCreateRequest(
            new NativeHorizontalWheelInput(
                short.MaxValue,
                0,
                new NativeScreenPoint(1, 1)),
            context,
            scrollCharacters:
                PreviewNativeHorizontalScrollPolicy.MaximumScrollCharacters,
            out PreviewNativeScrollRequest extreme));
        Assert.AreEqual(2000, extreme.DeltaX, 0.0001);

        Assert.IsTrue(_policy.TryCreateRequest(
            new NativeHorizontalWheelInput(120, 0, new NativeScreenPoint(1, 1)),
            context,
            PreviewNativeHorizontalScrollPolicy.PageScroll,
            out PreviewNativeScrollRequest page));
        Assert.AreEqual(500, page.DeltaX, 0.0001);
    }

    [TestMethod]
    public void LoadingErrorDisposedNavigationAndNonPreviewPointer_AreIgnored()
    {
        foreach (PreviewNativeInputState state in new[]
                 {
                     PreviewNativeInputState.Loading,
                     PreviewNativeInputState.Error,
                     PreviewNativeInputState.Disposed
                 })
        {
            Assert.IsFalse(TryCreate(ReadyContext() with { State = state }));
        }

        Assert.IsFalse(TryCreate(
            ReadyContext() with { IsNavigating = true }));
        Assert.IsFalse(TryCreate(
            ReadyContext() with { IsPointerOverPreview = false }));
    }

    [TestMethod]
    public void CurrentTokenIsRequiredAndControlNeverEntersNativeFallback()
    {
        Assert.IsFalse(TryCreate(
            ReadyContext() with { NavigationToken = null }));
        Assert.IsFalse(TryCreate(
            ReadyContext() with { NavigationToken = "stale" }));

        NativeHorizontalWheelInput controlInput = new(
            120,
            KeyState: 0x0008,
            new NativeScreenPoint(1, 1));
        Assert.IsFalse(_policy.TryCreateRequest(
            controlInput,
            ReadyContext(),
            scrollCharacters: 3,
            out _));
    }

    [TestMethod]
    public void ShiftIsAcceptedAndDistinctEventsAreNeverTimeDeduplicated()
    {
        NativeHorizontalWheelInput shifted = new(
            120,
            KeyState: 0x0004,
            new NativeScreenPoint(1, 1));
        Assert.IsTrue(_policy.TryCreateRequest(
            shifted,
            ReadyContext(),
            scrollCharacters: 3,
            out PreviewNativeScrollRequest first));
        Assert.IsTrue(_policy.TryCreateRequest(
            shifted,
            ReadyContext(),
            scrollCharacters: 3,
            out PreviewNativeScrollRequest second));
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void ZeroScrollSettingAndInvalidViewport_AreIgnored()
    {
        Assert.IsFalse(_policy.TryCreateRequest(
            new NativeHorizontalWheelInput(120, 0, new NativeScreenPoint(1, 1)),
            ReadyContext(),
            scrollCharacters: 0,
            out _));
        Assert.IsFalse(TryCreate(
            ReadyContext() with { ViewportWidth = double.NaN }));
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(1.5)]
    public void PreviewScreenHitTesting_UsesPhysicalDpiScaledBounds(double scale)
    {
        PreviewScreenHitTester hitTester = new();
        Assert.IsTrue(hitTester.Contains(
            new NativeScreenPoint(
                (int)((100 + (400 * scale)) - 1),
                (int)((200 + (300 * scale)) - 1)),
            previewLeftPixels: 100,
            previewTopPixels: 200,
            previewWidthDips: 400,
            previewHeightDips: 300,
            dpiScaleX: scale,
            dpiScaleY: scale));
        Assert.IsFalse(hitTester.Contains(
            new NativeScreenPoint(
                (int)(100 + (400 * scale)),
                250),
            100,
            200,
            400,
            300,
            scale,
            scale));
    }

    [TestMethod]
    public void PointerOverSourceOrInspectorIsOutsidePreviewBounds()
    {
        PreviewScreenHitTester hitTester = new();
        Assert.IsFalse(hitTester.Contains(
            new NativeScreenPoint(300, 400),
            previewLeftPixels: 800,
            previewTopPixels: 100,
            previewWidthDips: 400,
            previewHeightDips: 500,
            dpiScaleX: 1,
            dpiScaleY: 1));
        Assert.IsFalse(hitTester.Contains(
            new NativeScreenPoint(700, 400),
            800,
            100,
            400,
            500,
            1,
            1));
    }

    private bool TryCreate(PreviewNativeScrollContext context)
    {
        return _policy.TryCreateRequest(
            new NativeHorizontalWheelInput(
                120,
                0,
                new NativeScreenPoint(1, 1)),
            context,
            scrollCharacters: 3,
            out _);
    }

    private static PreviewNativeScrollContext ReadyContext(
        double viewportWidth = 600)
    {
        return new PreviewNativeScrollContext(
            PreviewNativeInputState.Ready,
            IsNavigating: false,
            IsPointerOverPreview: true,
            BridgeToken,
            viewportWidth);
    }

    private static nint PackWheelParameters(short delta, int keyState)
    {
        long value = unchecked(
            (ushort)keyState | ((long)(ushort)delta << 16));
        return (nint)value;
    }

    private static nint PackScreenPoint(short x, short y)
    {
        long value = unchecked((ushort)x | ((long)(ushort)y << 16));
        return (nint)value;
    }
}
