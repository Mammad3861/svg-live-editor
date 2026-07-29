using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class NativeHorizontalWheelMessageParser
{
    public const int MouseHorizontalWheelMessage = 0x020E;
    public const int PointerHorizontalWheelMessage = 0x024F;
    private const int ValidMouseKeyStateMask = 0x007F;

    public bool TryParse(
        int message,
        nint wParam,
        nint lParam,
        out NativeHorizontalWheelInput input)
    {
        input = default;
        if (message != MouseHorizontalWheelMessage)
        {
            return false;
        }

        long parameters = wParam.ToInt64();
        int keyState = unchecked((ushort)parameters);
        short delta = unchecked((short)(parameters >> 16));
        if (delta == 0 || (keyState & ~ValidMouseKeyStateMask) != 0)
        {
            return false;
        }

        NativeScreenPoint point = ParseScreenPoint(lParam);
        input = new NativeHorizontalWheelInput(delta, keyState, point);
        return true;
    }

    public NativeScreenPoint ParseScreenPoint(nint lParam)
    {
        long coordinates = lParam.ToInt64();
        int x = unchecked((short)coordinates);
        int y = unchecked((short)(coordinates >> 16));
        return new NativeScreenPoint(x, y);
    }
}
