using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewPointerGestureArbiter
{
    public PreviewPointerGestureAction Resolve(
        PreviewPointerGestureInput input)
    {
        if (input.Button == 1)
        {
            return PreviewPointerGestureAction.Pan;
        }

        if (input.Button != 0)
        {
            return PreviewPointerGestureAction.None;
        }

        if (input.PanModeEnabled
            || input.ControlHeld
            || input.SpaceHeld)
        {
            return PreviewPointerGestureAction.Pan;
        }

        if (!input.StartedOnArtwork
            || !input.IsPrimary
            || !input.IsMouse
            || input.ShiftHeld
            || input.AltHeld
            || input.MetaHeld)
        {
            return PreviewPointerGestureAction.None;
        }

        return PreviewPointerGestureAction.OutboundDrag;
    }
}
